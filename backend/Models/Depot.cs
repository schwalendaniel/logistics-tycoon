namespace LogisticsGame.Api.Models;

public class Depot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CityName { get; set; } = string.Empty;
    public bool IsHome { get; set; }
    public int Capacity { get; set; } = 4;
}
