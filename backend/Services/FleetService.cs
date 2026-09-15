using LogisticsGame.Api.Models;

namespace LogisticsGame.Api.Services;

public class FleetService
{
    private readonly GameState _state;
    private readonly PersistenceService _persistence;
    private readonly IRoutingService _routing;

    public FleetService(GameState state, PersistenceService persistence, IRoutingService routing)
    {
        _state = state;
        _persistence = persistence;
        _routing = routing;
    }

    public (bool Ok, string Error) Refuel(Guid truckId)
    {
        lock (_state.Sync)
        {
            var truck = _state.Trucks.FirstOrDefault(t => t.Id == truckId);
            if (truck == null) return (false, "Fahrzeug existiert nicht.");
            if (truck.Status != TruckStatus.Idle) return (false, "Tanken nur im Idle-Status.");
            var depot = _state.Depots.FirstOrDefault(d => d.CityName.Equals(truck.CurrentCity, StringComparison.OrdinalIgnoreCase));

            var liters = truck.FuelCapacityLiters - truck.CurrentFuelLiters;
            if (liters < 1) return (false, "Tank ist voll.");

            var priceMultiplier = depot == null ? 1.8m : 0.85m;
            var cost = Math.Round((decimal)liters * GameEconomy.DieselPerLiter * priceMultiplier, 2);
            if (!_state.TryDebit(cost, LedgerCategory.Refuel, $"Tanken {truck.LicensePlate} ({liters:0.0} L)"))
            {
                return (false, "Nicht genug Guthaben zum Tanken.");
            }

            truck.CurrentFuelLiters = truck.FuelCapacityLiters;
            var location = depot == null ? "unterwegs" : $"im Depot {depot.CityName}";
            _state.AddLog($"{truck.LicensePlate} {location} getankt ({cost:N2} €).");
        }

        _ = _persistence.SaveAsync();
        return (true, "");
    }

    public async Task<(bool Ok, string Error)> ReturnToDepotAsync(Guid truckId)
    {
        Truck? truck;
        Depot? depot;
        Driver? driver;

        lock (_state.Sync)
        {
            truck = _state.Trucks.FirstOrDefault(t => t.Id == truckId);
            if (truck == null) return (false, "Fahrzeug existiert nicht.");
            if (truck.Status != TruckStatus.Idle) return (false, "Nur freie Fahrzeuge können ins Depot geschickt werden.");

            depot = _state.Depots
                .OrderBy(d => CityDistance(truck.CurrentCity, d.CityName))
                .FirstOrDefault(d => _state.Trucks.Count(other =>
                    other.CurrentCity == d.CityName && other.Status != TruckStatus.EnRoute && other.Id != truck.Id) < d.Capacity);
            if (depot == null) return (false, "Kein Depot hat mehr Stellplätze frei.");

            driver = _state.Drivers.FirstOrDefault(d => d.Id == truck.AssignedDriverId);
            if (driver == null || driver.Status != DriverStatus.Available)
            {
                return (false, "Für die Rückfahrt wird ein verfügbarer Fahrer benötigt.");
            }
        }

        if (!JobService.GermanCities.TryGetValue(truck.CurrentCity, out var origin) ||
            !JobService.GermanCities.TryGetValue(depot.CityName, out var destination))
        {
            return (false, "Für die Rückfahrt konnte keine Route gefunden werden.");
        }

        var route = await _routing.CalculateRouteAsync(origin, destination);
        if (route == null) return (false, "Für die Rückfahrt konnte keine Route berechnet werden.");

        lock (_state.Sync)
        {
            if (truck.Status != TruckStatus.Idle || driver.Status != DriverStatus.Available)
            {
                return (false, "Fahrzeug oder Fahrer ist inzwischen nicht mehr verfügbar.");
            }

            truck.Status = TruckStatus.EnRoute;
            driver.Status = DriverStatus.OnRoute;
            var relocationJob = new Job
            {
                Title = $"Rückfahrt ins Depot {depot.CityName}",
                OriginCity = truck.CurrentCity,
                DestinationCity = depot.CityName,
                ExpirationDate = GameClock.Now(_state.Company).AddDays(1),
                Status = JobStatus.Assigned
            };
            _state.Jobs.Add(relocationJob);
            var tourId = Guid.NewGuid();
            _state.ActiveTours[tourId] = new ActiveTour
            {
                Id = tourId,
                Job = relocationJob,
                Route = route,
                Truck = truck,
                Driver = driver
            };
            _state.AddLog($"{truck.LicensePlate} fährt ins Depot {depot.CityName}.");
        }

        _ = _persistence.SaveAsync();
        return (true, "");
    }

    private static double CityDistance(string from, string to)
    {
        if (!JobService.GermanCities.TryGetValue(from, out var fromPoint) ||
            !JobService.GermanCities.TryGetValue(to, out var toPoint)) return 0;

        var latitudeDelta = (fromPoint.Latitude - toPoint.Latitude) * 111;
        var longitudeDelta = (fromPoint.Longitude - toPoint.Longitude) * 70;
        return Math.Sqrt(latitudeDelta * latitudeDelta + longitudeDelta * longitudeDelta);
    }

    public (bool Ok, string Error) Maintain(Guid truckId, MaintenanceLevel level)
    {
        lock (_state.Sync)
        {
            var truck = _state.Trucks.FirstOrDefault(t => t.Id == truckId);
            if (truck == null) return (false, "Fahrzeug existiert nicht.");
            if (truck.Status != TruckStatus.Idle) return (false, "Wartung nur im Idle-Status.");
            if (!_state.Depots.Any(d => d.CityName.Equals(truck.CurrentCity, StringComparison.OrdinalIgnoreCase)))
            {
                return (false, "Wartung ist nur in einem eigenen Depot möglich.");
            }

            var (cost, _) = GameEconomy.MaintenanceQuote(truck.Type, level);
            if (!_state.TryDebit(cost, LedgerCategory.Maintenance, $"Wartung {level} {truck.LicensePlate}"))
            {
                return (false, "Nicht genug Guthaben für die Werkstatt.");
            }

            GameEconomy.ApplyMaintenance(truck, level);
            _state.AddLog($"Werkstatt: {truck.LicensePlate} → {level} ({cost:N2} €).");
        }

        _ = _persistence.SaveAsync();
        return (true, "");
    }

    public (bool Ok, string Error) BailOut(Guid truckId)
    {
        lock (_state.Sync)
        {
            var truck = _state.Trucks.FirstOrDefault(t => t.Id == truckId);
            if (truck == null) return (false, "Fahrzeug existiert nicht.");
            if (truck.Status != TruckStatus.Impounded) return (false, "Fahrzeug ist nicht beschlagnahmt.");

            var bail = 2_800m;
            if (!_state.TryDebit(bail, LedgerCategory.Bail, $"Kaution {truck.LicensePlate}"))
            {
                return (false, "Kaution kann nicht gezahlt werden.");
            }

            truck.Status = TruckStatus.Idle;
            truck.CurrentCity = _state.Company.HomeCity;
            var driver = _state.Drivers.FirstOrDefault(d => d.Id == truck.AssignedDriverId && d.Status == DriverStatus.Arrested);
            if (driver != null)
            {
                driver.Status = DriverStatus.Available;
                driver.Loyalty = Math.Max(10, driver.Loyalty - 8);
                driver.Morale = Math.Max(10, driver.Morale - 15);
            }

            _state.AddLog($"Kaution gezahlt: {truck.LicensePlate} ist wieder frei.");
        }

        _ = _persistence.SaveAsync();
        return (true, "");
    }

    public (bool Ok, string Error, decimal Amount) SellTruck(Guid truckId)
    {
        decimal amount;
        lock (_state.Sync)
        {
            var truck = _state.Trucks.FirstOrDefault(t => t.Id == truckId);
            if (truck == null) return (false, "Fahrzeug existiert nicht.", 0m);
            if (truck.Status != TruckStatus.Idle)
            {
                return (false, "Verkauf nur im Idle-Status möglich.", 0m);
            }

            amount = GameEconomy.ResaleValue(truck);
            _state.Trucks.Remove(truck);

            _state.PostLedger(amount, LedgerCategory.Sale, $"Verkauf {truck.LicensePlate}");
            _state.AddLog($"Verkauft: {truck.LicensePlate} für {amount:N2} €.");
        }

        _ = _persistence.SaveAsync();
        return (true, "", amount);
    }

    public (bool Ok, string Error) BuyTruck(string catalogId)
    {
        var item = GameState.TruckCatalog.FirstOrDefault(c => c.CatalogId == catalogId);
        if (item == null) return (false, "Unbekanntes Fahrzeugangebot.");

        lock (_state.Sync)
        {
            var homeDepot = _state.Depots.FirstOrDefault(d => d.CityName.Equals(_state.Company.HomeCity, StringComparison.OrdinalIgnoreCase));
            var homeDepotTruckCount = _state.Trucks.Count(t => t.CurrentCity.Equals(_state.Company.HomeCity, StringComparison.OrdinalIgnoreCase));
            if (homeDepot != null && homeDepotTruckCount >= homeDepot.Capacity)
            {
                return (false, $"Das Depot {homeDepot.CityName} ist voll ({homeDepot.Capacity} Fahrzeuge).");
            }

            if (!_state.TryDebit(item.Price, LedgerCategory.Purchase, $"Kauf {item.ModelName}"))
            {
                return (false, "Zu wenig Kapital für den Kauf.");
            }

            var n = _state.Trucks.Count + 1;
            var plate = $"{_state.Company.HomeCity[0]}-LT {n:000}";
            var truck = new Truck
            {
                ModelName = item.ModelName,
                LicensePlate = plate,
                Type = item.Type,
                MaxPayloadTons = item.MaxPayloadTons,
                FuelCapacityLiters = item.FuelCapacityLiters,
                CurrentFuelLiters = item.FuelCapacityLiters * 0.7,
                EngineCondition = 100,
                TireCondition = 100,
                CabinCleanliness = 100,
                CurrentCity = _state.Company.HomeCity,
                PurchasePrice = item.Price
            };
            _state.Trucks.Add(truck);
            _state.AddLog($"Gekauft: {item.ModelName} ({plate}) für {item.Price:N0} €.");
        }

        _ = _persistence.SaveAsync();
        return (true, "");
    }

    public (bool Ok, string Error) BuyDepot(string cityName)
    {
        if (!JobService.GermanCities.ContainsKey(cityName)) return (false, "Stadt unbekannt.");
        if (!GameState.DepotMarketCities.Contains(cityName)) return (false, "Kein Depot-Angebot in dieser Stadt.");

        lock (_state.Sync)
        {
            if (_state.Depots.Any(d => d.CityName == cityName)) return (false, "Depot existiert bereits.");
            if (!_state.TryDebit(GameState.DepotPrice, LedgerCategory.Depot, $"Depot {cityName}"))
            {
                return (false, "Depot zu teuer.");
            }

            _state.Depots.Add(new Depot { CityName = cityName, IsHome = false, Capacity = 4 });
            _state.AddLog($"Neues Depot in {cityName}.");
        }

        _ = _persistence.SaveAsync();
        return (true, "");
    }
}
