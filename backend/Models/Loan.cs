namespace LogisticsGame.Api.Models;

public class Loan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Lender { get; set; } = string.Empty;
    public decimal Principal { get; set; }
    public decimal RemainingPrincipal { get; set; }
    public decimal MonthlyInterestRate { get; set; }
}

public enum LoanType
{
    Bank,
    LoanShark
}
