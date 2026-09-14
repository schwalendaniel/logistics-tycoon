using System.Collections.Concurrent;
using LogisticsGame.Api.Models;

namespace LogisticsGame.Api.Services;

public class ActiveTour
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Job Job { get; set; } = default!;
    public RouteInfo Route { get; set; } = default!;
    public Truck Truck { get; set; } = default!;
    public Driver Driver { get; set; } = default!;
    public int CurrentRouteIndex { get; set; } = 0;
    public double ProgressPercentage => Route.Geometry.Count == 0 ? 100 : (double)CurrentRouteIndex / (Route.Geometry.Count - 1) * 100.0;
    public bool IsFinished => CurrentRouteIndex >= Route.Geometry.Count - 1;
}

public class GameState
{
    public decimal CompanyBalance { get; set; } = 25000.00m;

    public List<Truck> Trucks { get; } = new()
    {
        new Truck 
        { 
            ModelName = "Hauler 40T", 
            LicensePlate = "K-LT 101", 
            Type = TruckType.SemiTruck,
            MaxPayloadTons = 24.0,
            FuelCapacityLiters = 600,
            CurrentFuelLiters = 450,
            EngineCondition = 95.0,
            TireCondition = 90.0,
            Status = TruckStatus.Idle
        },
        new Truck 
        { 
            ModelName = "Transporter 12T", 
            LicensePlate = "B-LT 202", 
            Type = TruckType.Rigid,
            MaxPayloadTons = 8.0,
            FuelCapacityLiters = 250,
            CurrentFuelLiters = 180,
            EngineCondition = 82.0,
            TireCondition = 75.0,
            Status = TruckStatus.Idle
        }
    };

    public List<Driver> Drivers { get; } = new()
    {
        new Driver 
        { 
            Name = "Markus Weber", 
            MonthlySalary = 3200m,
            CurrentSalary = 3200m,
            DrivingSkill = 75,
            Loyalty = 80,
            StressResistance = 60,
            Health = 100,
            Morale = 85,
            Status = DriverStatus.Available
        },
        new Driver 
        { 
            Name = "Elena Becker", 
            MonthlySalary = 3600m,
            CurrentSalary = 3600m,
            DrivingSkill = 88,
            Loyalty = 40, // Risikoreich bei Razzien!
            StressResistance = 75,
            Health = 95,
            Morale = 90,
            Status = DriverStatus.Available
        }
    };

    public ConcurrentDictionary<Guid, ActiveTour> ActiveTours { get; } = new();
    public ConcurrentQueue<string> EventLog { get; } = new();

    public void AddLog(string message)
    {
        EventLog.Enqueue($"[{DateTime.Now:HH:mm:ss}] {message}");
        while (EventLog.Count > 20)
        {
            EventLog.TryDequeue(out _);
        }
    }
}