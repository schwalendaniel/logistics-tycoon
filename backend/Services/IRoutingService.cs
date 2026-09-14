using LogisticsGame.Api.Models;

namespace LogisticsGame.Api.Services;

public interface IRoutingService
{
    Task<RouteInfo?> CalculateRouteAsync(Coordinates origin, Coordinates destination);
}