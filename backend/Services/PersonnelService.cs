using LogisticsGame.Api.Models;

namespace LogisticsGame.Api.Services;

public class PersonnelService
{
    private readonly GameState _state;
    private readonly PersistenceService _persistence;
    private readonly Random _random = new();

    private static readonly string[] FirstNames =
        ["Markus", "Elena", "Aylin", "Tomas", "Sabine", "Lukas", "Fatima", "Jonas", "Mira", "Stefan"];
    private static readonly string[] LastNames =
        ["Weber", "Becker", "Kaya", "Novak", "Hoffmann", "Schneider", "Yilmaz", "Krüger", "Bauer", "Wolf"];

    public PersonnelService(GameState state, PersistenceService persistence)
    {
        _state = state;
        _persistence = persistence;
        RefreshHirePool();
    }

    public void RefreshHirePool()
    {
        lock (_state.Sync)
        {
            _state.HirePool.Clear();
            for (var i = 0; i < 4; i++)
            {
                var d = new DriverProspect
                {
                    Name = $"{FirstNames[_random.Next(FirstNames.Length)]} {LastNames[_random.Next(LastNames.Length)]}",
                    DrivingSkill = _random.Next(28, 92),
                    Reliability = _random.Next(25, 90),
                    StressResistance = _random.Next(25, 90),
                    Loyalty = _random.Next(20, 95)
                };
                var avg = (d.DrivingSkill + d.Reliability + d.StressResistance + d.Loyalty) / 4.0;
                d.AskingSalary = Math.Round(2100m + (decimal)avg * 20m, 0);
                d.SigningFee = Math.Round(d.AskingSalary * 0.35m, 0);
                _state.HirePool.Add(d);
            }
        }
    }

    public (bool Ok, string Error) Hire(Guid prospectId)
    {
        lock (_state.Sync)
        {
            var prospect = _state.HirePool.FirstOrDefault(p => p.Id == prospectId);
            if (prospect == null) return (false, "Bewerber nicht mehr verfügbar.");
            if (!_state.TryDebit(prospect.SigningFee, LedgerCategory.Hire, $"Einstellung {prospect.Name}"))
            {
                return (false, "Handgeld kann nicht gezahlt werden.");
            }

            var driver = new Driver
            {
                Name = prospect.Name,
                DrivingSkill = prospect.DrivingSkill,
                Reliability = prospect.Reliability,
                StressResistance = prospect.StressResistance,
                Loyalty = prospect.Loyalty,
                MonthlySalary = prospect.AskingSalary,
                CurrentSalary = prospect.AskingSalary,
                Health = 100,
                Morale = 78,
                Status = DriverStatus.Available
            };
            driver.ExpectedSalary = GameEconomy.ExpectedSalaryFromSkills(driver);
            _state.Drivers.Add(driver);
            _state.HirePool.Remove(prospect);
            _state.AddLog($"Eingestellt: {driver.Name} ({driver.CurrentSalary:N0} €/Monat).");
        }

        _ = _persistence.SaveAsync();
        return (true, "");
    }

    public (bool Ok, string Error) AdjustSalary(Guid driverId, decimal salary)
    {
        if (salary < 1500m || salary > 8000m) return (false, "Gehalt außerhalb des Rahmens.");

        lock (_state.Sync)
        {
            var driver = _state.Drivers.FirstOrDefault(d => d.Id == driverId);
            if (driver == null) return (false, "Fahrer unbekannt.");
            driver.CurrentSalary = salary;
            driver.MonthlySalary = salary;
            if (salary >= driver.ExpectedSalary)
            {
                driver.Morale = Math.Min(100, driver.Morale + 8);
            }
            else
            {
                driver.Morale = Math.Max(5, driver.Morale - 6);
            }

            _state.AddLog($"Gehalt {driver.Name}: {salary:N0} €/Monat.");
        }

        _ = _persistence.SaveAsync();
        return (true, "");
    }

    public (bool Ok, string Error) PayBonus(Guid driverId, decimal amount)
    {
        if (amount < 50m || amount > 5000m) return (false, "Bonus ungültig.");

        lock (_state.Sync)
        {
            var driver = _state.Drivers.FirstOrDefault(d => d.Id == driverId);
            if (driver == null) return (false, "Fahrer unbekannt.");
            if (!_state.TryDebit(amount, LedgerCategory.Bonus, $"Bonus {driver.Name}"))
            {
                return (false, "Kein Geld für Bonus.");
            }

            driver.Morale = Math.Min(100, driver.Morale + (int)Math.Min(25, amount / 80m));
            driver.Loyalty = Math.Min(100, driver.Loyalty + 2);
            _state.AddLog($"Bonus an {driver.Name}: {amount:N0} €.");
        }

        _ = _persistence.SaveAsync();
        return (true, "");
    }

    public (bool Ok, string Error) ReleaseArrested(Guid driverId)
    {
        lock (_state.Sync)
        {
            var driver = _state.Drivers.FirstOrDefault(d => d.Id == driverId);
            if (driver == null) return (false, "Fahrer unbekannt.");
            if (driver.Status != DriverStatus.Arrested) return (false, "Fahrer ist nicht in Haft.");
            if (!_state.TryDebit(1_800m, LedgerCategory.Bail, $"Kaution Fahrer {driver.Name}"))
            {
                return (false, "Kaution zu teuer.");
            }

            driver.Status = DriverStatus.Available;
            driver.Morale = Math.Max(15, driver.Morale - 10);
            _state.AddLog($"{driver.Name} gegen Kaution aus der Haft geholt.");
        }

        _ = _persistence.SaveAsync();
        return (true, "");
    }
}
