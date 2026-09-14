using LogisticsGame.Api.Models;

namespace LogisticsGame.Api.Services;

public class JobService : IJobService
{
    private readonly IRoutingService _routingService;
    private readonly Random _random = new();

    public static readonly Dictionary<string, Coordinates> GermanCities = new()
    {
        { "Berlin", new Coordinates(52.5200, 13.4050) },
        { "Hamburg", new Coordinates(53.5511, 9.9937) },
        { "Köln", new Coordinates(50.9375, 6.9603) },
        { "Frankfurt", new Coordinates(50.1109, 8.6821) },
        { "München", new Coordinates(48.1372, 11.5755) },
        { "Stuttgart", new Coordinates(48.7758, 9.1829) },
        { "Leipzig", new Coordinates(51.3397, 12.3731) },
        { "Dortmund", new Coordinates(51.5136, 7.4653) },
        { "Nürnberg", new Coordinates(49.4521, 11.0767) },
        { "Bremen", new Coordinates(53.0793, 8.8017) }
    };

    private readonly string[] _legalCargo = { "Autoteile", "Maschinenbau-Komponenten", "Elektronik", "Lebensmittel", "Möbel", "Pharmaprodukte" };
    private readonly string[] _contrabandCargo = { "Unversteuerte Zigaretten", "Nicht deklarierte Luxusuhren", "Gefälschte Elektronik", "Schwarzmarkt-Medikamente" };

    public JobService(IRoutingService routingService)
    {
        _routingService = routingService;
    }

    public async Task<List<Job>> GenerateAvailableJobsAsync(int count = 5)
    {
        var cityNames = GermanCities.Keys.ToList();
        var jobs = new List<Job>();

        for (int i = 0; i < count; i++)
        {
            var origin = cityNames[_random.Next(cityNames.Count)];
            string dest;
            do {
                dest = cityNames[_random.Next(cityNames.Count)];
            } while (dest == origin);

            var route = await _routingService.CalculateRouteAsync(GermanCities[origin], GermanCities[dest]);
            var distance = route?.DistanceKm ?? 300.0;

            bool isContraband = _random.NextDouble() < 0.25; // 25% Chance auf Schwarzmarkt
            double weight = Math.Round(_random.NextDouble() * 20.0 + 1.5, 1);

            decimal baseRate = isContraband ? 4.2m : 1.9m; // Schwarzmarkt zahlt mehr als das Doppelte
            decimal revenue = Math.Round((decimal)distance * baseRate * (decimal)(1 + weight * 0.05), 2);

            jobs.Add(new Job
            {
                Id = Guid.NewGuid(),
                Title = isContraband 
                    ? $"[SCHWARZMARKT] {_contrabandCargo[_random.Next(_contrabandCargo.Length)]}" 
                    : _legalCargo[_random.Next(_legalCargo.Length)],
                OriginCity = origin,
                DestinationCity = dest,
                CargoWeightTons = weight,
                Revenue = revenue,
                IsIllegal = isContraband,
                InspectionRiskPercentage = isContraband ? Math.Round(_random.NextDouble() * 35.0 + 15.0, 1) : 0.0, // 15% - 50% Razzia-Risiko
                PenaltyFine = isContraband ? revenue * 2.5m : 0m,
                ExpirationDate = DateTime.UtcNow.AddMinutes(30),
                Status = JobStatus.Open
            });
        }

        return jobs;
    }
}