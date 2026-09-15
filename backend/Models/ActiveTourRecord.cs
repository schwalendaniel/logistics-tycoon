namespace LogisticsGame.Api.Models;

public class ActiveTourRecord
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public Guid TruckId { get; set; }
    public Guid DriverId { get; set; }
    public string RouteJson { get; set; } = "{}";
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
}
