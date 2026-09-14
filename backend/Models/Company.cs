namespace LogisticsGame.Api.Models;

public class Company
{
    public int Id { get; set; } = 1;
    public decimal Balance { get; set; } = 25_000m;
    public string HomeCity { get; set; } = "Frankfurt";
    public long TickCount { get; set; }
    public int GameDay { get; set; } = 1;
    public int GameHour { get; set; }
    public string EventLogJson { get; set; } = "[]";
}
