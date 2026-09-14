using LogisticsGame.Api.Models;

namespace LogisticsGame.Api.Services;

public class GameTickEngine : BackgroundService
{
    private readonly GameState _gameState;
    private readonly ILogger<GameTickEngine> _logger;
    private readonly Random _random = new();

    public GameTickEngine(GameState gameState, ILogger<GameTickEngine> logger)
    {
        _gameState = gameState;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            UpdateActiveTours();
        }
    }

    private void UpdateActiveTours()
    {
        foreach (var tour in _gameState.ActiveTours.Values)
        {
            if (tour.IsFinished) continue;

            tour.CurrentRouteIndex++;
            double stepDistance = tour.Route.DistanceKm / Math.Max(1, tour.Route.Geometry.Count);

            // 1. Kilometer & Verschleiß tracken
            tour.Truck.TotalKilometers += stepDistance;
            tour.Truck.TireCondition = Math.Max(0, tour.Truck.TireCondition - 0.05);
            tour.Truck.EngineCondition = Math.Max(0, tour.Truck.EngineCondition - 0.02);

            // Kabinenhygiene drückt die Fahrer-Gesundheit
            if (tour.Truck.CabinCleanliness < 40.0)
            {
                tour.Driver.Health = Math.Max(10, tour.Driver.Health - 1);
            }

            // 2. Spritverbrauch (DrivingSkill senkt Verbrauch bis zu 25%)
            double skillFactor = 1.0 - (tour.Driver.DrivingSkill / 400.0);
            double fuelBurned = (stepDistance * 0.32) * skillFactor; // ca. 32L/100km Basis
            tour.Truck.CurrentFuelLiters = Math.Max(0, tour.Truck.CurrentFuelLiters - fuelBurned);

            decimal fuelCost = (decimal)fuelBurned * 1.75m; // 1,75 €/Liter Diesel
            _gameState.CompanyBalance -= fuelCost;

            // 3. Schwarzmarkt-Zollrazzia (auf halber Strecke)
            if (tour.Job.IsIllegal && tour.CurrentRouteIndex == tour.Route.Geometry.Count / 2)
            {
                double roll = _random.NextDouble() * 100.0;
                if (roll <= tour.Job.InspectionRiskPercentage)
                {
                    // Zollkontrolle findet statt! Loyalty-Check: Hält der Fahrer dicht?
                    bool driverConfesses = (_random.Next(0, 100) > tour.Driver.Loyalty);

                    if (driverConfesses)
                    {
                        _gameState.CompanyBalance -= tour.Job.PenaltyFine;
                        tour.Truck.Status = TruckStatus.Impounded;
                        tour.Driver.Status = DriverStatus.Arrested;
                        tour.Job.Status = JobStatus.Failed;

                        _gameState.AddLog($"ZOLL-RAZZIA: {tour.Driver.Name} hat gestanden! LKW {tour.Truck.LicensePlate} beschlagnahmt. Strafe: -{tour.Job.PenaltyFine:N2} €");
                        _gameState.ActiveTours.TryRemove(tour.Id, out _);
                        continue;
                    }
                    else
                    {
                        _gameState.AddLog($"KONTROLLE: {tour.Driver.Name} blieb eiskalt! Die Fracht wurde nicht entdeckt.");
                    }
                }
            }

            // 4. Tour abgeschlossen
            if (tour.IsFinished)
            {
                tour.Job.Status = JobStatus.Completed;
                _gameState.CompanyBalance += tour.Job.Revenue;

                // Fahrer-Zustand nach Tour prüfen
                if (tour.Driver.Health < 30)
                {
                    tour.Driver.Status = DriverStatus.SickLeave;
                    tour.Driver.SickLeaveDaysRemaining = 3;
                    _gameState.AddLog($"Fahrer {tour.Driver.Name} meldet sich nach Tour krankheitsbedingt ab.");
                }
                else
                {
                    tour.Driver.Status = DriverStatus.Available;
                }

                tour.Truck.Status = TruckStatus.Idle;
                _gameState.AddLog($"Tour beendet: {tour.Job.Title} (+{tour.Job.Revenue:N2} €)");
                _gameState.ActiveTours.TryRemove(tour.Id, out _);
            }
        }
    }
}