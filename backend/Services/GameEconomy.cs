using LogisticsGame.Api.Models;

namespace LogisticsGame.Api.Services;

public static class GameEconomy
{
    public const decimal DieselPerLiter = 1.75m;
    public const int TargetOpenJobs = 6;
    public const int MaxEventLog = 40;

    public static double FuelLitersPerKm(TruckType type) => type switch
    {
        TruckType.Van => 0.10,
        TruckType.Rigid => 0.22,
        _ => 0.32
    };

    public static decimal TollPerKm(TruckType type) => type switch
    {
        TruckType.Van => 0m,
        TruckType.Rigid => 0.12m,
        _ => 0.19m
    };

    public static double SpeedFactor(TruckType type) => type switch
    {
        TruckType.Van => 0.82,
        TruckType.Rigid => 1.0,
        _ => 1.22
    };

    public static double ReliabilityDurationFactor(int reliability)
    {
        var clamped = Math.Clamp(reliability, 0, 100);
        return 1.0 + (50 - clamped) / 200.0;
    }

    public static decimal WearCostPerKm(Truck truck)
    {
        var wear = 1.0 + (100.0 - truck.EngineCondition) / 200.0;
        return 0.06m * (decimal)wear;
    }

    public static (decimal Cost, int Ticks) MaintenanceQuote(TruckType type, MaintenanceLevel level)
    {
        var typeMul = type switch
        {
            TruckType.Van => 0.55m,
            TruckType.Rigid => 1.0m,
            _ => 1.6m
        };

        return level switch
        {
            MaintenanceLevel.PatchJob => (Math.Round(380m * typeMul, 2), 8),
            MaintenanceLevel.Premium => (Math.Round(2650m * typeMul, 2), 36),
            _ => (Math.Round(1180m * typeMul, 2), 18)
        };
    }

    public static void ApplyMaintenance(Truck truck, MaintenanceLevel level)
    {
        truck.LastMaintenanceLevel = level;
        truck.Status = TruckStatus.Maintenance;
        var (_, ticks) = MaintenanceQuote(truck.Type, level);
        truck.MaintenanceTicksRemaining = ticks;

        switch (level)
        {
            case MaintenanceLevel.PatchJob:
                truck.TireCondition = Math.Min(85, truck.TireCondition + 35);
                truck.EngineCondition = Math.Min(70, truck.EngineCondition + 12);
                truck.CabinCleanliness = Math.Max(20, truck.CabinCleanliness - 8);
                break;
            case MaintenanceLevel.Premium:
                truck.TireCondition = 100;
                truck.EngineCondition = 100;
                truck.CabinCleanliness = 100;
                break;
            default:
                truck.TireCondition = Math.Min(92, Math.Max(truck.TireCondition, 88));
                truck.EngineCondition = Math.Min(90, Math.Max(truck.EngineCondition, 85));
                truck.CabinCleanliness = Math.Min(100, truck.CabinCleanliness + 25);
                break;
        }
    }

    public static decimal ExpectedSalaryFromSkills(Driver driver)
    {
        var avg = (driver.DrivingSkill + driver.Reliability + driver.StressResistance + driver.Loyalty) / 4.0;
        return Math.Round(2200m + (decimal)avg * 18m, 0);
    }

    public static decimal ResaleValue(Truck truck)
    {
        var averageCondition = Math.Clamp((truck.EngineCondition + truck.TireCondition) / 2.0, 0, 100);
        var conditionFactor = 0.35 + averageCondition / 100.0 * 0.65;
        var kilometerFactor = Math.Max(0.25, 1.0 - truck.TotalKilometers / 250_000.0 * 0.5);
        return Math.Round(truck.PurchasePrice * (decimal)(conditionFactor * kilometerFactor), 2);
    }
}
