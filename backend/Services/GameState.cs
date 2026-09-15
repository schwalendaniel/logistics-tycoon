using System.Collections.Concurrent;
using System.Text.Json;
using LogisticsGame.Api.Models;

namespace LogisticsGame.Api.Services;

public class ActiveTour
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Job Job { get; set; } = default!;
    public RouteInfo Route { get; set; } = default!;
    public Truck Truck { get; set; } = default!;
    public Driver Driver { get; set; } = default!;
    public double Progress { get; set; }
    public int CurrentRouteIndex { get; set; }
    public double AccumulatedFuelLiters { get; set; }
    public decimal AccumulatedFuelCost { get; set; }
    public decimal AccumulatedToll { get; set; }
    public decimal AccumulatedWear { get; set; }
    public double DeadheadKm { get; set; }
    public int BreakdownTicksRemaining { get; set; }
    public bool InspectionResolved { get; set; }
    public bool RecoveryRequested { get; set; }

    public double ProgressPercentage => Progress * 100.0;
    public bool IsFinished => Progress >= 1.0;
}

public class DriverProspect
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public int DrivingSkill { get; set; }
    public int Reliability { get; set; }
    public int StressResistance { get; set; }
    public int Loyalty { get; set; }
    public decimal AskingSalary { get; set; }
    public decimal SigningFee { get; set; }
}

public class TruckCatalogItem
{
    public string CatalogId { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public TruckType Type { get; set; }
    public double MaxPayloadTons { get; set; }
    public double FuelCapacityLiters { get; set; }
    public decimal Price { get; set; }
}

public class GameState
{
    public readonly object Sync = new();
    public const double DefaultSpeedMultiplier = 1.0;
    public const double MinSpeedMultiplier = 0.5;
    public const double MaxSpeedMultiplier = 10.0;
    public const double BaseTickIntervalSeconds = 3.0;

    public double SpeedMultiplier { get; set; } = DefaultSpeedMultiplier;

    public Company Company { get; set; } = new();

    public decimal CompanyBalance
    {
        get => Company.Balance;
        set => Company.Balance = value;
    }

    public List<Truck> Trucks { get; } = new();
    public List<Driver> Drivers { get; } = new();
    public List<Job> Jobs { get; } = new();
    public List<Depot> Depots { get; } = new();
    public List<LedgerEntry> Ledger { get; } = new();
    public ConcurrentDictionary<Guid, ActiveTour> ActiveTours { get; } = new();
    public ConcurrentQueue<string> EventLog { get; } = new();
    public List<DriverProspect> HirePool { get; } = new();
    public List<Loan> Loans { get; } = new();

    public static readonly TruckCatalogItem[] TruckCatalog =
    [
        new()
        {
            CatalogId = "van-courier",
            ModelName = "Courier 3.5T",
            Type = TruckType.Van,
            MaxPayloadTons = 1.2,
            FuelCapacityLiters = 75,
            Price = 18_500m
        },
        new()
        {
            CatalogId = "rigid-12",
            ModelName = "Transporter 12T",
            Type = TruckType.Rigid,
            MaxPayloadTons = 8.0,
            FuelCapacityLiters = 250,
            Price = 46_000m
        },
        new()
        {
            CatalogId = "semi-40",
            ModelName = "Hauler 40T",
            Type = TruckType.SemiTruck,
            MaxPayloadTons = 24.0,
            FuelCapacityLiters = 600,
            Price = 98_000m
        }
    ];

    public static readonly string[] DepotMarketCities = ["Hamburg", "München", "Leipzig"];
    public const decimal DepotPrice = 28_000m;

    public void SeedNewGame()
    {
        Company = new Company
        {
            Balance = 25_000m,
            HomeCity = "Frankfurt",
            GameDay = 1,
            GameHour = 8
        };

        Trucks.Clear();
        Drivers.Clear();
        Jobs.Clear();
        Depots.Clear();
        Ledger.Clear();
        Loans.Clear();
        ActiveTours.Clear();
        while (EventLog.TryDequeue(out _)) { }

        var driver = new Driver
        {
            Name = "Jonas Klein",
            MonthlySalary = 2400m,
            CurrentSalary = 2400m,
            ExpectedSalary = 2400m,
            DrivingSkill = 42,
            Reliability = 48,
            StressResistance = 40,
            Loyalty = 55,
            Health = 100,
            Morale = 72,
            Status = DriverStatus.Available
        };
        driver.ExpectedSalary = GameEconomy.ExpectedSalaryFromSkills(driver);
        Drivers.Add(driver);

        var van = new Truck
        {
            ModelName = "Courier 3.5T",
            LicensePlate = "F-LT 001",
            Type = TruckType.Van,
            MaxPayloadTons = 1.2,
            FuelCapacityLiters = 75,
            CurrentFuelLiters = 62,
            EngineCondition = 88,
            TireCondition = 80,
            CabinCleanliness = 90,
            Status = TruckStatus.Idle,
            CurrentCity = "Frankfurt",
            AssignedDriverId = driver.Id,
            PurchasePrice = 18_500m
        };
        Trucks.Add(van);

        Depots.Add(new Depot { CityName = "Frankfurt", IsHome = true, Capacity = 2 });
        AddLog("Neues Unternehmen in Frankfurt. Ein Van und Jonas Klein stehen bereit.");
        PostLedger(0m, LedgerCategory.Revenue, "Spielstart");
    }

    public int CreditScore
    {
        get
        {
            var debt = Loans.Sum(l => l.RemainingPrincipal);
            var score = 600 + (int)Math.Clamp(Company.Balance / 500m, -250, 250) - (int)Math.Clamp(debt / 500m, 0, 250);
            return Math.Clamp(score, 100, 850);
        }
    }

    public void SaveLoans()
    {
        Company.LoansJson = JsonSerializer.Serialize(Loans);
    }

    public void ResetAfterBankruptcy()
    {
        SeedNewGame();
        AddLog("Insolvenz abgeschlossen. Ein neuer Spielstand wurde gestartet.");
    }

    public ActiveTour? FindActiveTour(Guid tourId)
    {
        return ActiveTours.TryGetValue(tourId, out var tour) ? tour : null;
    }

    public void AddLog(string message)
    {
        EventLog.Enqueue($"[Tag {Company.GameDay} {Company.GameHour:00}:00] {message}");
        while (EventLog.Count > GameEconomy.MaxEventLog)
        {
            EventLog.TryDequeue(out _);
        }
    }

    public void PostLedger(decimal amount, LedgerCategory category, string description)
    {
        Company.Balance += amount;
        Ledger.Add(new LedgerEntry
        {
            Amount = amount,
            Category = category,
            Description = description
        });
        while (Ledger.Count > 80)
        {
            Ledger.RemoveAt(0);
        }
    }

    public bool TryDebit(decimal cost, LedgerCategory category, string description)
    {
        if (cost < 0) cost = 0;
        if (Company.Balance < cost) return false;
        PostLedger(-cost, category, description);
        return true;
    }
}
