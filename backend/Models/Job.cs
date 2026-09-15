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

public sealed class JobBoardItem
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string OriginCity { get; init; } = string.Empty;
    public string DestinationCity { get; init; } = string.Empty;
    public double CargoWeightTons { get; init; }
    public decimal Revenue { get; init; }
    public bool IsIllegal { get; init; }
    public double InspectionRiskPercentage { get; init; }
    public decimal PenaltyFine { get; init; }
    public DateTime ExpirationDate { get; init; }
    public double RemainingGameHours { get; init; }

    public static JobBoardItem From(Job job, DateTime now)
    {
        return new JobBoardItem
        {
            Id = job.Id,
            Title = job.Title,
            OriginCity = job.OriginCity,
            DestinationCity = job.DestinationCity,
            CargoWeightTons = job.CargoWeightTons,
            Revenue = job.Revenue,
            IsIllegal = job.IsIllegal,
            InspectionRiskPercentage = job.InspectionRiskPercentage,
            PenaltyFine = job.PenaltyFine,
            ExpirationDate = job.ExpirationDate,
            RemainingGameHours = Math.Round((job.ExpirationDate - now).TotalHours, 1)
        };
    }
}

public enum JobStatus
{
    Open,
    Assigned,
    Completed,
    Failed
}