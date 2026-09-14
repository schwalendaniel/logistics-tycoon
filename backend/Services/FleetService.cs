using LogisticsGame.Api.Models;

namespace LogisticsGame.Api.Services;

public class FleetService
{
    private readonly GameState _state;
    private readonly PersistenceService _persistence;

    public FleetService(GameState state, PersistenceService persistence)
    {
        _state = state;
        _persistence = persistence;
    }

    public (bool Ok, string Error) Refuel(Guid truckId)
    {
        lock (_state.Sync)
        {
            var truck = _state.Trucks.FirstOrDefault(t => t.Id == truckId);
            if (truck == null) return (false, "Fahrzeug existiert nicht.");
            if (truck.Status != TruckStatus.Idle) return (false, "Tanken nur im Idle-Status.");

            var liters = truck.FuelCapacityLiters - truck.CurrentFuelLiters;
            if (liters < 1) return (false, "Tank ist voll.");

            var cost = Math.Round((decimal)liters * GameEconomy.DieselPerLiter, 2);
            if (!_state.TryDebit(cost, LedgerCategory.Refuel, $"Tanken {truck.LicensePlate} ({liters:0.0} L)"))
            {
                return (false, "Nicht genug Guthaben zum Tanken.");
            }

            truck.CurrentFuelLiters = truck.FuelCapacityLiters;
            _state.AddLog($"Getankt: {truck.LicensePlate} ({cost:N2} €).");
        }

        _ = _persistence.SaveAsync();
        return (true, "");
    }

    public (bool Ok, string Error) Maintain(Guid truckId, MaintenanceLevel level)
    {
        lock (_state.Sync)
        {
            var truck = _state.Trucks.FirstOrDefault(t => t.Id == truckId);
            if (truck == null) return (false, "Fahrzeug existiert nicht.");
            if (truck.Status != TruckStatus.Idle) return (false, "Wartung nur im Idle-Status.");

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

    public (bool Ok, string Error) BuyTruck(string catalogId)
    {
        var item = GameState.TruckCatalog.FirstOrDefault(c => c.CatalogId == catalogId);
        if (item == null) return (false, "Unbekanntes Fahrzeugangebot.");

        lock (_state.Sync)
        {
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

            _state.Depots.Add(new Depot { CityName = cityName, IsHome = false });
            _state.AddLog($"Neues Depot in {cityName}.");
        }

        _ = _persistence.SaveAsync();
        return (true, "");
    }
}
