using LogisticsGame.Api.Models;
using LogisticsGame.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient<IRoutingService, OsrmRoutingService>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "LogisticsTycoonDevApp/1.0 (contact: local-dev)");
});

builder.Services.AddSingleton<IJobService, JobService>();
builder.Services.AddSingleton<GameState>();
builder.Services.AddHostedService<GameTickEngine>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// Frachtaufträge generieren
app.MapGet("/api/jobs", async (IJobService jobService) =>
{
    var jobs = await jobService.GenerateAvailableJobsAsync(4);
    return Results.Ok(jobs);
});

// Tour mit zugewiesenem LKW und Fahrer starten
app.MapPost("/api/tours/dispatch", async (DispatchTourRequest request, GameState gameState, IRoutingService routingService) =>
{
    var (job, truckId, driverId) = request;

    var truck = gameState.Trucks.FirstOrDefault(t => t.Id == truckId);
    if (truck == null) return Results.BadRequest("Fahrzeug existiert nicht.");
    if (truck.Status != TruckStatus.Idle) return Results.BadRequest($"Fahrzeug nicht frei (Status: {truck.Status}).");
    if (truck.MaxPayloadTons < job.CargoWeightTons) return Results.BadRequest($"Nutzlast zu gering ({truck.MaxPayloadTons}t < {job.CargoWeightTons}t).");
    if (truck.CurrentFuelLiters < 20.0) return Results.BadRequest("Zu wenig Kraftstoff im Tank.");

    var driver = gameState.Drivers.FirstOrDefault(d => d.Id == driverId);
    if (driver == null) return Results.BadRequest("Fahrer existiert nicht.");
    if (driver.Status != DriverStatus.Available) return Results.BadRequest($"Fahrer nicht verfügbar (Status: {driver.Status}).");

    if (!JobService.GermanCities.TryGetValue(job.OriginCity, out var origin) ||
        !JobService.GermanCities.TryGetValue(job.DestinationCity, out var dest))
    {
        return Results.BadRequest("Start- oder Zielstadt ungültig.");
    }

    var route = await routingService.CalculateRouteAsync(origin, dest);
    if (route == null) return Results.Problem("Route konnte nicht berechnet werden.");

    truck.Status = TruckStatus.EnRoute;
    driver.Status = DriverStatus.OnRoute;
    job.Status = JobStatus.Assigned;

    var tour = new ActiveTour
    {
        Job = job,
        Route = route,
        Truck = truck,
        Driver = driver
    };

    gameState.ActiveTours[tour.Id] = tour;
    gameState.AddLog($"Tour gestartet: {truck.LicensePlate} ({driver.Name}) ➔ {job.DestinationCity}");

    return Results.Ok(tour);
});

// Vollständiger State für Frontend (Konto, LKW, Fahrer, aktive Fahrten, Logs)
app.MapGet("/api/state", (GameState gameState) =>
{
    return Results.Ok(new
    {
        Balance = gameState.CompanyBalance,
        Trucks = gameState.Trucks.Select(t => new
        {
            t.Id,
            t.ModelName,
            t.LicensePlate,
            Type = t.Type.ToString(),
            t.MaxPayloadTons,
            t.FuelCapacityLiters,
            t.CurrentFuelLiters,
            t.EngineCondition,
            t.TireCondition,
            t.CabinCleanliness,
            t.TotalKilometers,
            Status = t.Status.ToString()
        }),
        Drivers = gameState.Drivers.Select(d => new
        {
            d.Id,
            d.Name,
            d.DrivingSkill,
            d.Loyalty,
            d.Health,
            d.Morale,
            d.CurrentSalary,
            Status = d.Status.ToString()
        }),
        ActiveTours = gameState.ActiveTours.Values.Select(t => new
        {
            t.Id,
            Title = t.Job.Title,
            IsIllegal = t.Job.IsIllegal,
            TruckPlate = t.Truck.LicensePlate,
            DriverName = t.Driver.Name,
            Progress = t.ProgressPercentage,
            CurrentPoint = t.Route.Geometry.ElementAtOrDefault(t.CurrentRouteIndex),
            FullGeometry = t.Route.Geometry
        }),
        Logs = gameState.EventLog.Reverse().Take(5)
    });
});

app.Run();