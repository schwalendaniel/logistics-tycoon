using LogisticsGame.Api.Models;

namespace LogisticsGame.Api.Services;

public class TourRecoveryService
{
    private readonly GameState _state;
    private readonly PersistenceService _persistence;

    public TourRecoveryService(GameState state, PersistenceService persistence)
    {
        _state = state;
        _persistence = persistence;
    }

    public (bool Ok, string Error) RecoverCargo(Guid? tourId, Guid brokenDownTruckId, Guid rescueTruckId, Guid rescueDriverId)
    {
        lock (_state.Sync)
        {
            var tour = tourId.HasValue && _state.ActiveTours.TryGetValue(tourId.Value, out var requestedTour)
                ? requestedTour
                : _state.ActiveTours.Values.FirstOrDefault(activeTour => activeTour.Truck.Id == brokenDownTruckId);
            if (tour == null) return (false, "Tour des defekten Fahrzeugs nicht gefunden.");
            if (tour.Truck.Status != TruckStatus.BrokenDown) return (false, "Der LKW ist nicht liegengeblieben.");

            var rescueTruck = _state.Trucks.FirstOrDefault(t => t.Id == rescueTruckId);
            if (rescueTruck == null) return (false, "Bergungsfahrzeug nicht gefunden.");
            if (rescueTruck.Status != TruckStatus.Idle) return (false, "Bergungsfahrzeug ist nicht frei.");
            if (rescueTruck.MaxPayloadTons < tour.Job.CargoWeightTons)
            {
                return (false, "Das Bergungsfahrzeug hat zu wenig Nutzlast.");
            }
            if (rescueTruck.Id == tour.Truck.Id) return (false, "Der defekte LKW kann sich nicht selbst bergen.");

            var rescueDriver = _state.Drivers.FirstOrDefault(d => d.Id == rescueDriverId);
            if (rescueDriver == null) return (false, "Bergungsfahrer nicht gefunden.");
            if (rescueDriver.Status != DriverStatus.Available) return (false, "Bergungsfahrer ist nicht verfügbar.");

            var cost = Math.Round(650m + (decimal)tour.Route.DistanceKm * 2.2m, 2);
            _state.PostLedger(-cost, LedgerCategory.Recovery, $"Ladungsbergung {tour.Job.Title}");
            tour.Job.Status = JobStatus.Completed;
            rescueTruck.CurrentCity = tour.Job.DestinationCity;
            rescueTruck.Status = TruckStatus.Idle;
            rescueTruck.AssignedDriverId = rescueDriver.Id;
            rescueDriver.Status = DriverStatus.Available;
            tour.Truck.Status = TruckStatus.BrokenDown;
            tour.Driver.Status = DriverStatus.Available;
            _state.ActiveTours.TryRemove(tour.Id, out _);
            _state.PostLedger(tour.Job.Revenue, LedgerCategory.Revenue, $"Bergungsauftrag {tour.Job.Title}");
            _state.AddLog($"Ladung geborgen und zugestellt: {tour.Job.Title} (-{cost:N2} €).");
        }

        _ = _persistence.SaveAsync();
        return (true, "");
    }

    public (bool Ok, string Error) TowTruck(Guid? tourId, Guid brokenDownTruckId)
    {
        lock (_state.Sync)
        {
            var tour = tourId.HasValue && _state.ActiveTours.TryGetValue(tourId.Value, out var requestedTour)
                ? requestedTour
                : _state.ActiveTours.Values.FirstOrDefault(activeTour => activeTour.Truck.Id == brokenDownTruckId);
            if (tour == null)
            {
                var strandedTruck = _state.Trucks.FirstOrDefault(truck => truck.Id == brokenDownTruckId);
                if (strandedTruck == null) return (false, "Defektes Fahrzeug nicht gefunden.");
                if (strandedTruck.Status != TruckStatus.BrokenDown) return (false, "Der LKW ist nicht liegengeblieben.");

                const decimal fallbackTowCost = 1_500m;
                if (!_state.TryDebit(fallbackTowCost, LedgerCategory.Towing, $"Abschleppen {strandedTruck.LicensePlate}"))
                {
                    return (false, "Nicht genug Guthaben für das Abschleppen.");
                }

                strandedTruck.Status = TruckStatus.Idle;
                strandedTruck.CurrentCity = _state.Company.HomeCity;
                strandedTruck.AssignedDriverId = null;
                _state.AddLog($"{strandedTruck.LicensePlate} ohne aktive Tour ins Heimatdepot abgeschleppt (-{fallbackTowCost:N2} €).");
                _ = _persistence.SaveAsync();
                return (true, "");
            }
            if (tour.Truck.Status != TruckStatus.BrokenDown) return (false, "Der LKW ist nicht liegengeblieben.");

            var cost = Math.Round(1_200m + (decimal)tour.Route.DistanceKm * 1.6m, 2);
            _state.PostLedger(-cost, LedgerCategory.Towing, $"Abschleppen {tour.Truck.LicensePlate}");
            tour.Truck.Status = TruckStatus.Idle;
            tour.Truck.CurrentCity = _state.Company.HomeCity;
            tour.Truck.AssignedDriverId = null;
            tour.Driver.Status = DriverStatus.Available;
            tour.Job.Status = JobStatus.Failed;
            _state.ActiveTours.TryRemove(tour.Id, out _);
            _state.AddLog($"{tour.Truck.LicensePlate} abgeschleppt (-{cost:N2} €). Ladung verloren.");
        }

        _ = _persistence.SaveAsync();
        return (true, "");
    }
}
