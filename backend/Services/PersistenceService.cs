using System.Text.Json;
using LogisticsGame.Api.Data;
using LogisticsGame.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace LogisticsGame.Api.Services;

public class PersistenceService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly GameState _state;
    private readonly ILogger<PersistenceService> _logger;
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public PersistenceService(IServiceScopeFactory scopeFactory, GameState state, ILogger<PersistenceService> logger)
    {
        _scopeFactory = scopeFactory;
        _state = state;
        _logger = logger;
    }

    public async Task LoadOrSeedAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        await db.Database.EnsureCreatedAsync();

        var company = await db.Companies.AsNoTracking().FirstOrDefaultAsync();
        if (company == null)
        {
            _state.SeedNewGame();
            await SaveAsync();
            return;
        }

        lock (_state.Sync)
        {
            _state.Company = company;
            _state.Trucks.Clear();
            _state.Trucks.AddRange(db.Trucks.AsNoTracking().ToList());
            _state.Drivers.Clear();
            _state.Drivers.AddRange(db.Drivers.AsNoTracking().ToList());
            _state.Jobs.Clear();
            _state.Jobs.AddRange(db.Jobs.AsNoTracking().ToList());
            _state.Depots.Clear();
            _state.Depots.AddRange(db.Depots.AsNoTracking().ToList());
            _state.Ledger.Clear();
            _state.Ledger.AddRange(db.Ledger.AsNoTracking().OrderBy(l => l.Timestamp).ToList());
            _state.ActiveTours.Clear();

            try
            {
                var logs = JsonSerializer.Deserialize<List<string>>(company.EventLogJson, JsonOptions);
                if (logs != null)
                {
                    foreach (var log in logs)
                    {
                        _state.EventLog.Enqueue(log);
                    }
                }
            }
            catch (JsonException)
            {
                // ignore corrupt log blob
            }

            foreach (var record in db.Tours.AsNoTracking().ToList())
            {
                var truck = _state.Trucks.FirstOrDefault(t => t.Id == record.TruckId);
                var driver = _state.Drivers.FirstOrDefault(d => d.Id == record.DriverId);
                var job = _state.Jobs.FirstOrDefault(j => j.Id == record.JobId);
                if (truck == null || driver == null || job == null) continue;

                RouteInfo? route;
                try
                {
                    route = JsonSerializer.Deserialize<RouteInfo>(record.RouteJson, JsonOptions);
                }
                catch (JsonException)
                {
                    continue;
                }

                if (route == null) continue;

                _state.ActiveTours[record.Id] = new ActiveTour
                {
                    Id = record.Id,
                    Job = job,
                    Truck = truck,
                    Driver = driver,
                    Route = route,
                    Progress = record.Progress,
                    CurrentRouteIndex = record.CurrentRouteIndex,
                    AccumulatedFuelLiters = record.AccumulatedFuelLiters,
                    AccumulatedFuelCost = record.AccumulatedFuelCost,
                    AccumulatedToll = record.AccumulatedToll,
                    AccumulatedWear = record.AccumulatedWear,
                    DeadheadKm = record.DeadheadKm,
                    BreakdownTicksRemaining = record.BreakdownTicksRemaining,
                    InspectionResolved = record.InspectionResolved
                };
            }
        }

        _logger.LogInformation("Spielstand geladen ({Trucks} LKW, {Jobs} Jobs).", _state.Trucks.Count, _state.Jobs.Count);
    }

    public async Task SaveAsync()
    {
        await _saveLock.WaitAsync();
        try
        {
            await SaveCoreAsync();
        }
        finally
        {
            _saveLock.Release();
        }
    }

    private async Task SaveCoreAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();

        List<Truck> trucks;
        List<Driver> drivers;
        List<Job> jobs;
        List<Depot> depots;
        List<LedgerEntry> ledger;
        List<ActiveTourRecord> tours;
        Company company;

        lock (_state.Sync)
        {
            company = CloneCompany(_state.Company);
            company.EventLogJson = JsonSerializer.Serialize(_state.EventLog.ToArray(), JsonOptions);
            trucks = _state.Trucks.Select(CloneTruck).ToList();
            drivers = _state.Drivers.Select(CloneDriver).ToList();
            jobs = _state.Jobs.Select(CloneJob).ToList();
            depots = _state.Depots.Select(d => new Depot { Id = d.Id, CityName = d.CityName, IsHome = d.IsHome }).ToList();
            ledger = _state.Ledger.Select(l => new LedgerEntry
            {
                Id = l.Id,
                Timestamp = l.Timestamp,
                Description = l.Description,
                Amount = l.Amount,
                Category = l.Category
            }).ToList();
            tours = _state.ActiveTours.Values.Select(t => new ActiveTourRecord
            {
                Id = t.Id,
                JobId = t.Job.Id,
                TruckId = t.Truck.Id,
                DriverId = t.Driver.Id,
                RouteJson = JsonSerializer.Serialize(t.Route, JsonOptions),
                Progress = t.Progress,
                CurrentRouteIndex = t.CurrentRouteIndex,
                AccumulatedFuelLiters = t.AccumulatedFuelLiters,
                AccumulatedFuelCost = t.AccumulatedFuelCost,
                AccumulatedToll = t.AccumulatedToll,
                AccumulatedWear = t.AccumulatedWear,
                DeadheadKm = t.DeadheadKm,
                BreakdownTicksRemaining = t.BreakdownTicksRemaining,
                InspectionResolved = t.InspectionResolved
            }).ToList();
        }

        await using var tx = await db.Database.BeginTransactionAsync();
        db.Tours.RemoveRange(await db.Tours.ToListAsync());
        db.Ledger.RemoveRange(await db.Ledger.ToListAsync());
        db.Jobs.RemoveRange(await db.Jobs.ToListAsync());
        db.Trucks.RemoveRange(await db.Trucks.ToListAsync());
        db.Drivers.RemoveRange(await db.Drivers.ToListAsync());
        db.Depots.RemoveRange(await db.Depots.ToListAsync());
        db.Companies.RemoveRange(await db.Companies.ToListAsync());
        await db.SaveChangesAsync();

        db.Companies.Add(company);
        db.Trucks.AddRange(trucks);
        db.Drivers.AddRange(drivers);
        db.Jobs.AddRange(jobs);
        db.Depots.AddRange(depots);
        db.Ledger.AddRange(ledger);
        db.Tours.AddRange(tours);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    private static Company CloneCompany(Company c) => new()
    {
        Id = 1,
        Balance = c.Balance,
        HomeCity = c.HomeCity,
        TickCount = c.TickCount,
        GameDay = c.GameDay,
        GameHour = c.GameHour,
        EventLogJson = c.EventLogJson
    };

    private static Truck CloneTruck(Truck t) => new()
    {
        Id = t.Id,
        ModelName = t.ModelName,
        LicensePlate = t.LicensePlate,
        Type = t.Type,
        MaxPayloadTons = t.MaxPayloadTons,
        FuelCapacityLiters = t.FuelCapacityLiters,
        CurrentFuelLiters = t.CurrentFuelLiters,
        EngineCondition = t.EngineCondition,
        TireCondition = t.TireCondition,
        TotalKilometers = t.TotalKilometers,
        AssignedDriverId = t.AssignedDriverId,
        Status = t.Status,
        CabinCleanliness = t.CabinCleanliness,
        LastMaintenanceLevel = t.LastMaintenanceLevel,
        CurrentCity = t.CurrentCity,
        MaintenanceTicksRemaining = t.MaintenanceTicksRemaining,
        PurchasePrice = t.PurchasePrice
    };

    private static Driver CloneDriver(Driver d) => new()
    {
        Id = d.Id,
        Name = d.Name,
        MonthlySalary = d.MonthlySalary,
        DrivingSkill = d.DrivingSkill,
        Reliability = d.Reliability,
        StressResistance = d.StressResistance,
        Loyalty = d.Loyalty,
        Health = d.Health,
        Morale = d.Morale,
        CurrentSalary = d.CurrentSalary,
        ExpectedSalary = d.ExpectedSalary,
        SickLeaveDaysRemaining = d.SickLeaveDaysRemaining,
        Status = d.Status
    };

    private static Job CloneJob(Job j) => new()
    {
        Id = j.Id,
        Title = j.Title,
        OriginCity = j.OriginCity,
        DestinationCity = j.DestinationCity,
        CargoWeightTons = j.CargoWeightTons,
        Revenue = j.Revenue,
        IsIllegal = j.IsIllegal,
        InspectionRiskPercentage = j.InspectionRiskPercentage,
        PenaltyFine = j.PenaltyFine,
        ExpirationDate = j.ExpirationDate,
        Status = j.Status
    };
}
