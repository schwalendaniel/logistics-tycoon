namespace LogisticsGame.Api.Models;

public class Truck
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ModelName { get; set; } = string.Empty; // z. B. "Hauler 40T", kein Markenname
    public string LicensePlate { get; set; } = string.Empty; 
    public TruckType Type { get; set; }
    public double MaxPayloadTons { get; set; }
    public double FuelCapacityLiters { get; set; }
    public double CurrentFuelLiters { get; set; }

    // Zustand & Verschleiß (100 = Fabrikneu, 0 = Schrott)
    public double EngineCondition { get; set; } = 100.0;
    public double TireCondition { get; set; } = 100.0;
    public double TotalKilometers { get; set; } = 0.0;

    // Zugewiesener Fahrer
    public Guid? AssignedDriverId { get; set; }

    public TruckStatus Status { get; set; } = TruckStatus.Idle;

    // Zustand & Kabinenhygiene
    public double CabinCleanliness { get; set; } = 100.0; // Filter/Klima-Zustand (beeinflusst Fahrer-Gesundheit)
    public MaintenanceLevel LastMaintenanceLevel { get; set; } = MaintenanceLevel.Standard;
}

public enum TruckType
{
    Van,       // 3.5t Sprinter (schnell, mautfrei, kleine Fracht)
    Rigid,     // 12t Verteiler-Lkw
    SemiTruck  // 40t Sattelzug (hohe Fracht, teure Maut)
}

public enum TruckStatus
{
    Idle,
    Loading,
    EnRoute,
    Maintenance,
    Impounded // Beschlagnahmt nach Schwarzmarkt-Razzia
}

public enum MaintenanceLevel
{
    PatchJob,    // Nur das Nötigste (billig, miese Kabinenluft)
    Standard,    // Normale Inspektion
    Premium      // Alles neu inkl. Pollenfilter, AC-Service, Ergonomie-Check
}