using LogisticsGame.Api.Models;

namespace LogisticsGame.Api.Services;

public class TourDispatchService
{
    private readonly GameState _state;
    private readonly IRoutingService _routing;
    private readonly PersistenceService _persistence;

    public TourDispatchService(GameState state, IRoutingService routing, PersistenceService persistence)
    {
        _state = state;
        _routing = routing;
        _persistence = persistence;
    }

    public async Task<(bool Ok, string Error, ActiveTour? Tour)> DispatchAsync(Guid jobId, Guid truckId, Guid driverId)
    {
        Job? job;
        Truck? truck;
        Driver? driver;

        lock (_state.Sync)
        {
            job = _state.Jobs.FirstOrDefault(j => j.Id == jobId);
            truck = _state.Trucks.FirstOrDefault(t => t.Id == truckId);
            driver = _state.Drivers.FirstOrDefault(d => d.Id == driverId);

            if (job == null) return (false, "Auftrag nicht gefunden.", null);
            if (job.Status != JobStatus.Open) return (false, "Auftrag ist nicht mehr offen.", null);
            if (job.ExpirationDate < GameClock.Now(_state.Company)) return (false, "Auftrag abgelaufen.", null);
            if (truck == null) return (false, "Fahrzeug existiert nicht.", null);
            if (truck.Status != TruckStatus.Idle) return (false, $"Fahrzeug nicht frei (Status: {truck.Status}).", null);
            if (truck.MaxPayloadTons < job.CargoWeightTons)
            {
                return (false, $"Nutzlast zu gering ({truck.MaxPayloadTons}t < {job.CargoWeightTons}t).", null);
            }

            if (driver == null) return (false, "Fahrer existiert nicht.", null);
            if (driver.Status == DriverStatus.SickLeave) return (false, "Fahrer ist krankgeschrieben.", null);
            if (driver.Status != DriverStatus.Available) return (false, $"Fahrer nicht verfügbar (Status: {driver.Status}).", null);

            // Krankfeiern: niedrige Moral trotz Gesundheit
            if (driver.Morale < 28 && driver.Health >= 40)
            {
                var malingerChance = (28 - driver.Morale) / 40.0;
                if (Random.Shared.NextDouble() < malingerChance)
                {
                    driver.Status = DriverStatus.SickLeave;
                    driver.SickLeaveDaysRemaining = Math.Max(1, 3 - driver.Loyalty / 40);
                    _state.AddLog($"{driver.Name} ruft kurzfristig krank (Krankfeiern).");
                    return (false, $"{driver.Name} hat sich krank gemeldet.", null);
                }
            }
        }

        if (!JobService.GermanCities.TryGetValue(job.OriginCity, out var origin) ||
            !JobService.GermanCities.TryGetValue(job.DestinationCity, out var dest))
        {
            return (false, "Start- oder Zielstadt ungültig.", null);
        }

        var cargoRoute = await _routing.CalculateRouteAsync(origin, dest);
        if (cargoRoute == null) return (false, "Route konnte nicht berechnet werden.", null);

        double deadheadKm = 0;
        var geometry = new List<RoutePoint>(cargoRoute.Geometry);
        var duration = cargoRoute.EstimatedDurationMinutes;
        var distance = cargoRoute.DistanceKm;

        string? fromCity;
        lock (_state.Sync)
        {
            fromCity = truck!.CurrentCity;
        }

        if (!string.IsNullOrWhiteSpace(fromCity) &&
            !fromCity.Equals(job.OriginCity, StringComparison.OrdinalIgnoreCase) &&
            JobService.GermanCities.TryGetValue(fromCity, out var fromCoords))
        {
            var deadhead = await _routing.CalculateRouteAsync(fromCoords, origin);
            if (deadhead != null && deadhead.Geometry.Count > 0)
            {
                deadheadKm = deadhead.DistanceKm;
                geometry = deadhead.Geometry.Concat(cargoRoute.Geometry).ToList();
                duration += deadhead.EstimatedDurationMinutes;
                distance += deadhead.DistanceKm;
            }
        }

        var combined = new RouteInfo
        {
            DistanceKm = Math.Round(distance, 1),
            EstimatedDurationMinutes = Math.Round(duration, 1),
            Geometry = geometry
        };

        ActiveTour tour;
        lock (_state.Sync)
        {
            truck!.Status = TruckStatus.EnRoute;
            truck.AssignedDriverId = driver!.Id;
            driver.Status = DriverStatus.OnRoute;
            job!.Status = JobStatus.Assigned;

            tour = new ActiveTour
            {
                Job = job,
                Route = combined,
                Truck = truck,
                Driver = driver,
                DeadheadKm = deadheadKm
            };

            _state.ActiveTours[tour.Id] = tour;
            var deadheadNote = deadheadKm > 1 ? $" inkl. {deadheadKm:0.0} km Leerfahrt" : "";
            _state.AddLog($"Tour gestartet: {truck.LicensePlate} ({driver.Name}) ➔ {job.DestinationCity}{deadheadNote}");
        }

        await _persistence.SaveAsync();
        return (true, "", tour);
    }
}
