namespace LogisticsGame.Api.Models;

public class Driver
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public decimal MonthlySalary { get; set; }

    // RPG-Skills (Wertebereich 0 bis 100)
    public int DrivingSkill { get; set; } = 50;      // Senkt Spritverbrauch & Unfallrisiko
    public int Reliability { get; set; } = 50;       // Pünktlichkeit vs. Trödeln
    public int StressResistance { get; set; } = 50;  // Beeinflusst Erschöpfung bei langen Touren
    public int Loyalty { get; set; } = 50;           // Wie dicht hält er bei Zoll-/Polizeikontrollen?
    // Gesundheit, Zufriedenheit & Vergütung
    public int Health { get; set; } = 100;              // 0 = Intensivstation, 100 = Topfit
    public int Morale { get; set; } = 80;               // 0 = Streikbereit / Krankfeiern, 100 = Hochmotiviert
    public decimal CurrentSalary { get; set; }          // Tatsächlich gezahlter Lohn
    public decimal ExpectedSalary { get; set; }         // Marktüblicher Anspruch laut Skillset
    public int SickLeaveDaysRemaining { get; set; } = 0; // Sperrzeit bei Krankheit

    public DriverStatus Status { get; set; } = DriverStatus.Available;
}

public enum DriverStatus
{
    Available,
    OnRoute,
    Resting,
    SickLeave,
    Arrested
}