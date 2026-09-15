using LogisticsGame.Api.Models;

namespace LogisticsGame.Api.Services;

public class GameTickEngine : BackgroundService
{
    private readonly GameState _state;
    private readonly PersistenceService _persistence;
    private readonly FinanceService _finance;
    private readonly ILogger<GameTickEngine> _logger;
    private readonly Random _random = new();

    public GameTickEngine(GameState state, PersistenceService persistence, FinanceService finance, ILogger<GameTickEngine> logger)
    {
        _state = state;
        _persistence = persistence;
        _finance = finance;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                double tickIntervalSeconds;
                lock (_state.Sync)
                {
                    tickIntervalSeconds = GameState.BaseTickIntervalSeconds / _state.SpeedMultiplier;
                }

                await Task.Delay(TimeSpan.FromSeconds(Math.Max(0.05, tickIntervalSeconds)), stoppingToken);
                TickOnce();
                if (_state.Company.TickCount % 10 == 0)
                {
                    await _persistence.SaveAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tick fehlgeschlagen");
            }
        }
    }

    private void TickOnce()
    {
        lock (_state.Sync)
        {
            if (_state.Company.GameOver) return;
            _state.Company.TickCount++;
            AdvanceClock();
            AdvanceMaintenance();
            UpdateActiveTours();
        }
    }

    private void AdvanceClock()
    {
        GameClock.Advance(_state.Company);
        var newGameDay = _state.Company.GameHour == 0;

        foreach (var driver in _state.Drivers)
        {
            if (driver.Status == DriverStatus.SickLeave)
            {
                driver.SickLeaveDaysRemaining = Math.Max(0, driver.SickLeaveDaysRemaining - 1);
                if (driver.SickLeaveDaysRemaining == 0)
                {
                    driver.Status = DriverStatus.Available;
                    driver.Health = Math.Max(driver.Health, 55);
                    _state.AddLog($"{driver.Name} ist wieder einsatzbereit.");
                }
            }

            if (driver.Status == DriverStatus.Arrested)
            {
                driver.SickLeaveDaysRemaining = Math.Max(0, driver.SickLeaveDaysRemaining - 1);
                if (driver.SickLeaveDaysRemaining == 0)
                {
                    driver.Status = DriverStatus.Available;
                    _state.AddLog($"{driver.Name} wurde aus der Haft entlassen.");
                }
            }

            if (driver.Status is DriverStatus.Available or DriverStatus.Resting)
            {
                driver.Health = Math.Min(100, driver.Health + 2);
            }

            if (driver.CurrentSalary + 50m < driver.ExpectedSalary)
            {
                driver.Morale = Math.Max(0, driver.Morale - 3);
            }
            else if (driver.CurrentSalary >= driver.ExpectedSalary)
            {
                driver.Morale = Math.Min(100, driver.Morale + 1);
            }

            var assigned = _state.Trucks.FirstOrDefault(t => t.AssignedDriverId == driver.Id);
            if (assigned != null && assigned.LastMaintenanceLevel == MaintenanceLevel.Premium && assigned.CabinCleanliness > 70)
            {
                driver.Morale = Math.Min(100, driver.Morale + 1);
                driver.Health = Math.Min(100, driver.Health + 1);
            }
        }

        if (newGameDay && _state.Company.GameDay % 30 == 0)
        {
            PayMonthlySalaries();
            _finance.ChargeMonthlyInterest();
        }
    }

    private void PayMonthlySalaries()
    {
        decimal total = 0;
        foreach (var driver in _state.Drivers.Where(d => d.Status != DriverStatus.Arrested))
        {
            total += driver.CurrentSalary;
        }

        if (total <= 0) return;
        _state.PostLedger(-total, LedgerCategory.Wage, $"Monatsgehälter Tag {_state.Company.GameDay}");
        _state.AddLog($"Lohnlauf: -{total:N2} €");
    }

    private void AdvanceMaintenance()
    {
        foreach (var truck in _state.Trucks.Where(t => t.Status == TruckStatus.Maintenance))
        {
            truck.MaintenanceTicksRemaining--;
            if (truck.MaintenanceTicksRemaining > 0) continue;
            truck.Status = TruckStatus.Idle;
            _state.AddLog($"Wartung fertig: {truck.LicensePlate}");
        }
    }

    private void UpdateActiveTours()
    {
        foreach (var tour in _state.ActiveTours.Values.ToList())
        {
            if (tour.IsFinished)
            {
                CompleteTour(tour, seized: false);
                continue;
            }

            if (tour.Truck.Status == TruckStatus.BrokenDown)
            {
                continue;
            }

            if (tour.Truck.CurrentFuelLiters <= 0)
            {
                RefuelOnRoad(tour);
                continue;
            }

            var durationMinutes = Math.Max(12, tour.Route.EstimatedDurationMinutes);
            durationMinutes *= GameEconomy.SpeedFactor(tour.Truck.Type);
            durationMinutes *= GameEconomy.ReliabilityDurationFactor(tour.Driver.Reliability);

            var minutesPerTick = (double)GameClock.MinutesPerTick;
            var delta = minutesPerTick / durationMinutes;
            var previous = tour.Progress;
            tour.Progress = Math.Min(1.0, tour.Progress + delta);

            var geomCount = Math.Max(2, tour.Route.Geometry.Count);
            tour.CurrentRouteIndex = Math.Clamp(
                (int)Math.Round(tour.Progress * (geomCount - 1)),
                0,
                geomCount - 1);

            var stepKm = tour.Route.DistanceKm * delta;
            tour.Truck.TotalKilometers += stepKm;
            tour.Truck.TireCondition = Math.Max(0, tour.Truck.TireCondition - stepKm * 0.012);
            tour.Truck.EngineCondition = Math.Max(0, tour.Truck.EngineCondition - stepKm * 0.006);
            tour.Truck.CabinCleanliness = Math.Max(0, tour.Truck.CabinCleanliness - stepKm * 0.008);

            if (tour.Truck.CabinCleanliness < 40.0)
            {
                tour.Driver.Health = Math.Max(5, tour.Driver.Health - 1);
            }

            var hours = minutesPerTick / 60.0;
            var stressHit = Math.Max(0, (70 - tour.Driver.StressResistance) / 80.0);
            tour.Driver.Health = Math.Max(5, tour.Driver.Health - (int)Math.Round(hours * (0.4 + stressHit)));

            var skillFactor = 1.0 - (tour.Driver.DrivingSkill / 400.0);
            var fuelBurned = stepKm * GameEconomy.FuelLitersPerKm(tour.Truck.Type) * skillFactor;
            if (tour.Truck.EngineCondition < 50) fuelBurned *= 1.15;

            if (tour.Truck.CurrentFuelLiters < fuelBurned)
            {
                RefuelOnRoad(tour);
            }

            tour.Truck.CurrentFuelLiters = Math.Max(0, tour.Truck.CurrentFuelLiters - fuelBurned);
            tour.AccumulatedFuelLiters += fuelBurned;
            tour.AccumulatedFuelCost += (decimal)fuelBurned * GameEconomy.DieselPerLiter;
            tour.AccumulatedToll += GameEconomy.TollPerKm(tour.Truck.Type) * (decimal)stepKm;
            tour.AccumulatedWear += GameEconomy.WearCostPerKm(tour.Truck) * (decimal)stepKm;

            MaybeBreakdown(tour);
            MaybeInspect(tour, previous);
            if (tour.Truck.Status == TruckStatus.Impounded || !_state.ActiveTours.ContainsKey(tour.Id))
            {
                continue;
            }

            if (tour.IsFinished)
            {
                CompleteTour(tour, seized: false);
            }
        }
    }

    private void MaybeBreakdown(ActiveTour tour)
    {
        var risk = 0.0;
        if (tour.Truck.EngineCondition < 35) risk += 0.04;
        if (tour.Truck.TireCondition < 25) risk += 0.05;
        risk *= 1.0 - tour.Driver.DrivingSkill / 140.0;
        if (risk <= 0 || _random.NextDouble() > risk) return;

        tour.Truck.Status = TruckStatus.BrokenDown;
        tour.BreakdownTicksRemaining = 0;
        tour.Driver.Morale = Math.Max(5, tour.Driver.Morale - 4);
        _state.AddLog($"Panne: {tour.Truck.LicensePlate} ({tour.Driver.Name}) — LKW steht. Bergung oder Abschleppen erforderlich.");
    }

    private void RefuelOnRoad(ActiveTour tour)
    {
        var liters = Math.Max(25.0, tour.Truck.FuelCapacityLiters * 0.35);
        var cost = Math.Round((decimal)liters * GameEconomy.DieselPerLiter * 1.8m, 2);
        _state.PostLedger(-cost, LedgerCategory.RoadsideFuel, $"Notbetankung unterwegs {tour.Truck.LicensePlate}");
        tour.AccumulatedFuelCost += cost;
        tour.Truck.CurrentFuelLiters = Math.Min(tour.Truck.FuelCapacityLiters, liters);
        _state.AddLog($"{tour.Truck.LicensePlate} wurde unterwegs notbetankt: -{cost:N2} €.");
    }

    private void MaybeInspect(ActiveTour tour, double previousProgress)
    {
        if (!tour.Job.IsIllegal || tour.InspectionResolved) return;
        if (!(previousProgress < 0.5 && tour.Progress >= 0.5)) return;

        tour.InspectionResolved = true;
        var roll = _random.NextDouble() * 100.0;
        if (roll > tour.Job.InspectionRiskPercentage)
        {
            return;
        }

        var driverConfesses = _random.Next(0, 100) > tour.Driver.Loyalty;
        if (!driverConfesses)
        {
            _state.AddLog($"KONTROLLE: {tour.Driver.Name} blieb eiskalt! Die Fracht wurde nicht entdeckt.");
            return;
        }

        _state.PostLedger(-tour.Job.PenaltyFine, LedgerCategory.Fine, $"Strafe {tour.Job.Title}");
        tour.Truck.Status = TruckStatus.Impounded;
        tour.Driver.Status = DriverStatus.Arrested;
        tour.Driver.SickLeaveDaysRemaining = 4;
        tour.Job.Status = JobStatus.Failed;
        _state.AddLog($"ZOLL-RAZZIA: {tour.Driver.Name} hat gestanden! {tour.Truck.LicensePlate} beschlagnahmt. Strafe: -{tour.Job.PenaltyFine:N2} €");
        _state.ActiveTours.TryRemove(tour.Id, out _);
    }

    private void FailTour(ActiveTour tour)
    {
        tour.Job.Status = JobStatus.Failed;
        tour.Truck.Status = TruckStatus.Idle;
        tour.Truck.CurrentCity = tour.Job.OriginCity;
        FinishDriverAfterTour(tour.Driver, tour);
        _state.ActiveTours.TryRemove(tour.Id, out _);
    }

    private void CompleteTour(ActiveTour tour, bool seized)
    {
        if (seized) return;
        if (tour.Truck.Status is TruckStatus.Impounded or TruckStatus.BrokenDown) return;

        tour.Job.Status = JobStatus.Completed;
        tour.Truck.Status = TruckStatus.Idle;
        tour.Truck.CurrentCity = tour.Job.DestinationCity;

        var fuel = Math.Round(tour.AccumulatedFuelCost, 2);
        var toll = Math.Round(tour.AccumulatedToll, 2);
        var wear = Math.Round(tour.AccumulatedWear, 2);
        var isLate = GameClock.Now(_state.Company) > tour.Job.ExpirationDate;
        var revenue = isLate ? Math.Round(tour.Job.Revenue * 0.8m, 2) : tour.Job.Revenue;
        var net = revenue - fuel - toll - wear;

        _state.PostLedger(revenue, LedgerCategory.Revenue, isLate ? $"{tour.Job.Title} (verspätet, -20 %)" : tour.Job.Title);
        if (fuel > 0) _state.PostLedger(-fuel, LedgerCategory.Fuel, $"Diesel {tour.Truck.LicensePlate}");
        if (toll > 0) _state.PostLedger(-toll, LedgerCategory.Toll, $"Maut {tour.Truck.LicensePlate}");
        if (wear > 0) _state.PostLedger(-wear, LedgerCategory.Wear, $"Verschleiß {tour.Truck.LicensePlate}");

        FinishDriverAfterTour(tour.Driver, tour);
        var deadhead = tour.DeadheadKm > 1 ? $" | Leerfahrt {tour.DeadheadKm:0.0} km" : "";
        var lateNote = isLate ? " | verspätet (-20 % Umsatz)" : "";
        _state.AddLog($"Tour beendet: {tour.Job.Title} | Netto {net:N2} €{lateNote}{deadhead}");
        _state.ActiveTours.TryRemove(tour.Id, out _);
    }

    private void FinishDriverAfterTour(Driver driver, ActiveTour tour)
    {
        if (driver.Status == DriverStatus.Arrested) return;

        var durationHours = Math.Max(1, tour.Route.EstimatedDurationMinutes / 60.0);
        if (durationHours > 6) driver.Morale = Math.Max(0, driver.Morale - 4);

        if (driver.Health < 30)
        {
            driver.Status = DriverStatus.SickLeave;
            driver.SickLeaveDaysRemaining = 2 + (30 - driver.Health) / 10;
            _state.AddLog($"{driver.Name} ist nach der Tour krankgeschrieben ({driver.SickLeaveDaysRemaining} Tage).");
            return;
        }

        if (driver.Morale < 22 && driver.Health >= 40 && _random.NextDouble() < 0.45)
        {
            driver.Status = DriverStatus.SickLeave;
            driver.SickLeaveDaysRemaining = 1;
            _state.AddLog($"{driver.Name} macht blau (niedrige Moral).");
            return;
        }

        driver.Status = DriverStatus.Available;
    }
}
