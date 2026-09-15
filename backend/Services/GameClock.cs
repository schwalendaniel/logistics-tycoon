using LogisticsGame.Api.Models;

namespace LogisticsGame.Api.Services;

public static class GameClock
{
    public const int MinutesPerTick = 60;
    public const int HoursPerDay = 24;
    public const int JobLifetimeHours = 45;

    public static bool IsLegacyRealWorldTimestamp(DateTime timestamp)
    {
        return timestamp.Year >= 2000;
    }

    public static DateTime Now(Company company)
    {
        return DateTime.UnixEpoch
            .AddDays(Math.Max(0, company.GameDay - 1))
            .AddHours(Math.Clamp(company.GameHour, 0, HoursPerDay - 1));
    }

    public static void Advance(Company company)
    {
        company.GameHour += MinutesPerTick / 60;
        if (company.GameHour < HoursPerDay) return;

        company.GameHour = 0;
        company.GameDay++;
    }
}