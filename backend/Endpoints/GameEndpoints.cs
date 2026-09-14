using LogisticsGame.Api.Models;
using LogisticsGame.Api.Services;

namespace LogisticsGame.Api.Endpoints;

public static class GameEndpoints
{
    public static void MapGameEndpoints(this WebApplication app)
    {
        app.MapGet("/api/jobs", async (IJobService jobs, GameState state) =>
        {
            await jobs.EnsureBoardAsync();
            lock (state.Sync)
            {
                return Results.Ok(state.Jobs.Where(j => j.Status == JobStatus.Open).ToList());
            }
        });

        app.MapPost("/api/jobs/refresh", async (IJobService jobs, GameState state) =>
        {
            lock (state.Sync)
            {
                foreach (var job in state.Jobs.Where(j => j.Status == JobStatus.Open))
                {
                    job.Status = JobStatus.Failed;
                }
            }

            await jobs.EnsureBoardAsync();
            lock (state.Sync)
            {
                return Results.Ok(state.Jobs.Where(j => j.Status == JobStatus.Open).ToList());
            }
        });

        app.MapPost("/api/tours/dispatch", async (DispatchTourRequest request, TourDispatchService tours) =>
        {
            var (ok, error, tour) = await tours.DispatchAsync(request.JobId, request.TruckId, request.DriverId);
            if (!ok || tour == null) return Results.BadRequest(error);
            return Results.Ok(new { tour.Id, Progress = tour.ProgressPercentage });
        });

        

        app.MapPost("/api/fleet/refuel", (RefuelRequest request, FleetService fleet) =>
            ToResult(fleet.Refuel(request.TruckId)));

        app.MapPost("/api/fleet/maintain", (MaintainRequest request, FleetService fleet) =>
            ToResult(fleet.Maintain(request.TruckId, request.Level)));

        app.MapPost("/api/fleet/bail", (BailRequest request, FleetService fleet) =>
            ToResult(fleet.BailOut(request.TruckId)));

        app.MapPost("/api/fleet/buy", (BuyTruckRequest request, FleetService fleet) =>
            ToResult(fleet.BuyTruck(request.CatalogId)));

        app.MapPost("/api/depots/buy", (BuyDepotRequest request, FleetService fleet) =>
            ToResult(fleet.BuyDepot(request.CityName)));

        app.MapGet("/api/market", (GameState state, PersonnelService personnel) =>
        {
            if (state.HirePool.Count == 0) personnel.RefreshHirePool();
            lock (state.Sync)
            {
                return Results.Ok(new
                {
                    Trucks = GameState.TruckCatalog.Select(c => new
                    {
                        c.CatalogId,
                        c.ModelName,
                        Type = c.Type.ToString(),
                        c.MaxPayloadTons,
                        c.Price
                    }),
                    Drivers = state.HirePool,
                    Depots = GameState.DepotMarketCities
                        .Where(city => state.Depots.All(d => d.CityName != city))
                        .Select(city => new { CityName = city, Price = GameState.DepotPrice })
                });
            }
        });

        app.MapPost("/api/market/hire-refresh", (PersonnelService personnel) =>
        {
            personnel.RefreshHirePool();
            return Results.Ok();
        });

        app.MapPost("/api/personnel/hire", (HireDriverRequest request, PersonnelService personnel) =>
            ToResult(personnel.Hire(request.ProspectId)));

        app.MapPost("/api/personnel/salary", (AdjustSalaryRequest request, PersonnelService personnel) =>
            ToResult(personnel.AdjustSalary(request.DriverId, request.MonthlySalary)));

        app.MapPost("/api/personnel/bonus", (BonusRequest request, PersonnelService personnel) =>
            ToResult(personnel.PayBonus(request.DriverId, request.Amount)));

        app.MapPost("/api/personnel/bail", (DriverIdRequest request, PersonnelService personnel) =>
            ToResult(personnel.ReleaseArrested(request.DriverId)));

        app.MapGet("/api/state", (GameState state) =>
        {
            lock (state.Sync)
            {
                return Results.Ok(new
                {
                    Balance = state.CompanyBalance,
                    Day = state.Company.GameDay,
                    Hour = state.Company.GameHour,
                    HomeCity = state.Company.HomeCity,
                    Depots = state.Depots.Select(d => new
                    {
                        d.CityName,
                        d.IsHome,
                        Location = JobService.GermanCities.TryGetValue(d.CityName, out var c)
                            ? c
                            : new Coordinates(0, 0)
                    }),
                    Trucks = state.Trucks.Select(t => new
                    {
                        t.Id,
                        t.ModelName,
                        t.LicensePlate,
                        Type = t.Type.ToString(),
                        t.MaxPayloadTons,
                        t.FuelCapacityLiters,
                        t.CurrentFuelLiters,
                        t.EngineCondition,
                        t.TireCondition,
                        t.CabinCleanliness,
                        t.TotalKilometers,
                        Status = t.Status.ToString(),
                        t.CurrentCity,
                        LastMaintenance = t.LastMaintenanceLevel.ToString(),
                        t.AssignedDriverId,
                        t.MaintenanceTicksRemaining
                    }),
                    Drivers = state.Drivers.Select(d => new
                    {
                        d.Id,
                        d.Name,
                        d.DrivingSkill,
                        d.Reliability,
                        d.StressResistance,
                        d.Loyalty,
                        d.Health,
                        d.Morale,
                        d.CurrentSalary,
                        d.ExpectedSalary,
                        d.SickLeaveDaysRemaining,
                        Status = d.Status.ToString()
                    }),
                    ActiveTours = state.ActiveTours.Values.Select(t => new
                    {
                        t.Id,
                        Title = t.Job.Title,
                        IsIllegal = t.Job.IsIllegal,
                        TruckPlate = t.Truck.LicensePlate,
                        DriverName = t.Driver.Name,
                        Progress = t.ProgressPercentage,
                        t.DeadheadKm,
                        CurrentPoint = t.Route.Geometry.ElementAtOrDefault(t.CurrentRouteIndex),
                        FullGeometry = t.Route.Geometry
                    }),
                    Ledger = state.Ledger.AsEnumerable().Reverse().Take(12).Select(l => new
                    {
                        l.Description,
                        l.Amount,
                        Category = l.Category.ToString()
                    }),
                    Logs = state.EventLog.Reverse().Take(12)
                });
            }
        });
    }

    private static IResult ToResult((bool Ok, string Error) result)
        => result.Ok ? Results.Ok() : Results.BadRequest(result.Error);
}
