namespace LogisticsGame.Api.Models;

public enum LedgerCategory
{
    Revenue,
    Fuel,
    Toll,
    Wear,
    Wage,
    Maintenance,
    Refuel,
    Fine,
    Bail,
    Purchase,
    Hire,
    Bonus,
    Depot,
    Sale,
    Loan,
    Interest,
    Bankruptcy,
    RoadsideFuel,
    Recovery,
    Towing,
    Severance
}

public class LedgerEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public LedgerCategory Category { get; set; }
}
