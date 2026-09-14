namespace LogisticsGame.Api.Models;

public record Coordinates(double Latitude, double Longitude);

public class City
{
    public string Name { get; set; } = string.Empty;
    public Coordinates Location { get; set; } = new(0, 0);
}