using LogisticsGame.Api.Models;

namespace LogisticsGame.Api.Services;

public interface IJobService
{
    Task<List<Job>> GenerateJobsAsync(int count);
    Task EnsureBoardAsync(int targetOpen = GameEconomy.TargetOpenJobs);
}

public class JobService : IJobService
{
    private readonly IRoutingService _routingService;
    private readonly GameState _gameState;
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

    private readonly string[] _legalCargo = ["Autoteile", "Maschinenbau-Komponenten", "Elektronik", "Lebensmittel", "Möbel", "Pharmaprodukte"];
    private readonly string[] _contrabandCargo = ["Unversteuerte Zigaretten", "Nicht deklarierte Luxusuhren", "Gefälschte Elektronik", "Schwarzmarkt-Medikamente"];

    public JobService(IRoutingService routingService, GameState gameState)
    {
        _routingService = routingService;
        _gameState = gameState;
    }

    public async Task EnsureBoardAsync(int targetOpen = GameEconomy.TargetOpenJobs)
    {
        ExpireJobs();
        int open;
        lock (_gameState.Sync)
        {
            open = _gameState.Jobs.Count(j => j.Status == JobStatus.Open);
        }

        if (open >= targetOpen) return;

        var generated = await GenerateJobsAsync(targetOpen - open);
        lock (_gameState.Sync)
        {
            _gameState.Jobs.AddRange(generated);
        }
    }

    public void ExpireJobs()
    {
        lock (_gameState.Sync)
        {
            var now = GameClock.Now(_gameState.Company);
            foreach (var job in _gameState.Jobs.Where(j => j.Status == JobStatus.Open))
            {
                if (GameClock.IsLegacyRealWorldTimestamp(job.ExpirationDate))
                {
                    job.ExpirationDate = now.AddHours(GameClock.JobLifetimeHours);
                    continue;
                }

                if (job.ExpirationDate >= now) continue;
                job.Status = JobStatus.Failed;
            }

            _gameState.Jobs.RemoveAll(j =>
                j.Status is JobStatus.Failed or JobStatus.Completed &&
                j.ExpirationDate < now.AddHours(-2));
        }
    }

    public async Task<List<Job>> GenerateJobsAsync(int count)
    {
        var cityNames = GermanCities.Keys.ToList();
        var jobs = new List<Job>();
        var maxPayload = 24.0;
        lock (_gameState.Sync)
        {
            if (_gameState.Trucks.Count > 0)
            {
                maxPayload = _gameState.Trucks.Max(t => t.MaxPayloadTons);
            }
        }

        for (var i = 0; i < count; i++)
        {
            var origin = cityNames[_random.Next(cityNames.Count)];
            string dest;
            do
            {
                dest = cityNames[_random.Next(cityNames.Count)];
            } while (dest == origin);

            var route = await _routingService.CalculateRouteAsync(GermanCities[origin], GermanCities[dest]);
            var distance = route?.DistanceKm ?? 300.0;

            var isContraband = _random.NextDouble() < 0.22;
            var weightCap = Math.Max(1.1, maxPayload);
            var weight = Math.Round(_random.NextDouble() * Math.Min(18.0, weightCap * 0.9) + 0.4, 1);
            if (weight > weightCap) weight = Math.Round(weightCap * 0.7, 1);

            var baseRate = isContraband ? 4.2m : 1.9m;
            var revenue = Math.Round((decimal)distance * baseRate * (decimal)(1 + weight * 0.05), 2);

            jobs.Add(new Job
            {
                Title = isContraband
                    ? $"[SCHWARZMARKT] {_contrabandCargo[_random.Next(_contrabandCargo.Length)]}"
                    : _legalCargo[_random.Next(_legalCargo.Length)],
                OriginCity = origin,
                DestinationCity = dest,
                CargoWeightTons = weight,
                Revenue = revenue,
                IsIllegal = isContraband,
                InspectionRiskPercentage = isContraband ? Math.Round(_random.NextDouble() * 35.0 + 15.0, 1) : 0.0,
                PenaltyFine = isContraband ? revenue * 2.5m : 0m,
                ExpirationDate = GameClock.Now(_gameState.Company).AddHours(GameClock.JobLifetimeHours),
                Status = JobStatus.Open
            });
        }

        return jobs;
    }
}
