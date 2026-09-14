namespace LogisticsGame.Api.Models;

public class Job
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string OriginCity { get; set; } = string.Empty;
    public string DestinationCity { get; set; } = string.Empty;
    public double CargoWeightTons { get; set; }
    public decimal Revenue { get; set; }

    // Schwarzmarkt & Risiko
    public bool IsIllegal { get; set; } = false;
    public double InspectionRiskPercentage { get; set; } = 0.0; // Wahrscheinlichkeit einer Razzia
    public decimal PenaltyFine { get; set; } = 0m;              // Strafe bei Entdeckung

    // Laufzeit
    public DateTime ExpirationDate { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Open;
}

public enum JobStatus
{
    Open,
    Assigned,
    Completed,
    Failed
}