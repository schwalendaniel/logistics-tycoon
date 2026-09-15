using LogisticsGame.Api.Data;
using LogisticsGame.Api.Endpoints;
using LogisticsGame.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

builder.Services.AddHttpClient("osrm", client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "LogisticsTycoonDevApp/1.0 (contact: local-dev)");
});
builder.Services.AddSingleton<IRoutingService>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    return new OsrmRoutingService(factory.CreateClient("osrm"));
});

var dbPath = Path.Combine(builder.Environment.ContentRootPath, "game.db");
builder.Services.AddDbContext<GameDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddSingleton<GameState>();
builder.Services.AddSingleton<PersistenceService>();
builder.Services.AddSingleton<IJobService, JobService>();
builder.Services.AddSingleton<FleetService>();
builder.Services.AddSingleton<PersonnelService>();
builder.Services.AddSingleton<FinanceService>();
builder.Services.AddSingleton<TourDispatchService>();
builder.Services.AddSingleton<TourRecoveryService>();
builder.Services.AddHostedService<GameTickEngine>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

await app.Services.GetRequiredService<PersistenceService>().LoadOrSeedAsync();

app.MapGameEndpoints();
app.Run();
