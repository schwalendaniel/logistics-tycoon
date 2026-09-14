namespace LogisticsGame.Api.Models;

public record RoutePoint(double Latitude, double Longitude);

public class RouteInfo
{
    public double DistanceKm { get; set; }
    public double EstimatedDurationMinutes { get; set; }
    public List<RoutePoint> Geometry { get; set; } = new();
}