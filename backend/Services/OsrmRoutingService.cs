using System.Text.Json;
using System.Text.Json.Nodes;
using LogisticsGame.Api.Models;

namespace LogisticsGame.Api.Services;

public class OsrmRoutingService : IRoutingService
{
    private readonly HttpClient _httpClient;

    public OsrmRoutingService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<RouteInfo?> CalculateRouteAsync(Coordinates origin, Coordinates destination)
    {
        // OSRM erwartet Koordinaten im Format: {longitude},{latitude}
       var url = $"https://router.project-osrm.org/route/v1/driving/{origin.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)},{origin.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)};{destination.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)},{destination.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}?overview=simplified&geometries=geojson";
       var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        var node = JsonNode.Parse(json);

        var routes = node?["routes"]?.AsArray();
        if (routes == null || routes.Count == 0) return null;

        var primaryRoute = routes[0];
        var distanceMeters = primaryRoute?["distance"]?.GetValue<double>() ?? 0;
        var durationSeconds = primaryRoute?["duration"]?.GetValue<double>() ?? 0;

        var coordinatesArray = primaryRoute?["geometry"]?["coordinates"]?.AsArray();
        var points = new List<RoutePoint>();

        if (coordinatesArray != null)
        {
            foreach (var coord in coordinatesArray)
            {
                var lon = coord?[0]?.GetValue<double>() ?? 0;
                var lat = coord?[1]?.GetValue<double>() ?? 0;
                points.Add(new RoutePoint(lat, lon));
            }
        }

        return new RouteInfo
        {
            DistanceKm = Math.Round(distanceMeters / 1000.0, 1),
            EstimatedDurationMinutes = Math.Round(durationSeconds / 60.0, 1),
            Geometry = points
        };
    }
}