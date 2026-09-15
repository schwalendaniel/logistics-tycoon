using LogisticsGame.Api.Models;

namespace LogisticsGame.Api.Services;

public class FinanceService
{
    private readonly GameState _state;
    private readonly PersistenceService _persistence;

    public FinanceService(GameState state, PersistenceService persistence)
    {
        _state = state;
        _persistence = persistence;
    }

    public (bool Ok, string Error, object? Details) Borrow(LoanType type, decimal amount)
    {
        lock (_state.Sync)
        {
            if (_state.Company.GameOver) return (false, "Das Unternehmen ist zahlungsunfähig.", null);
            var offer = GetOffer(type);
            if (offer.MaxAmount <= 0) return (false, offer.Reason, null);
            if (amount <= 0 || amount > offer.MaxAmount) return (false, $"Maximal möglich: {offer.MaxAmount:N0} €.", null);

            var loan = new Loan
            {
                Lender = offer.Lender,
                Principal = amount,
                RemainingPrincipal = amount,
                MonthlyInterestRate = offer.MonthlyRate
            };
            _state.Loans.Add(loan);
            _state.PostLedger(amount, LedgerCategory.Loan, $"Kreditaufnahme {offer.Lender}");
            _state.AddLog($"Kredit aufgenommen: {amount:N0} € bei {offer.Lender} ({offer.MonthlyRate:0.0}%/Monat).");
            _state.SaveLoans();
        }

        _ = _persistence.SaveAsync();
        return (true, "", new { Amount = amount });
    }

    public (bool Ok, string Error) Repay(Guid loanId, decimal amount)
    {
        lock (_state.Sync)
        {
            var loan = _state.Loans.FirstOrDefault(l => l.Id == loanId);
            if (loan == null) return (false, "Kredit nicht gefunden.");
            amount = Math.Round(Math.Clamp(amount, 0, loan.RemainingPrincipal), 2);
            if (amount <= 0) return (false, "Ungültiger Rückzahlungsbetrag.");
            if (!_state.TryDebit(amount, LedgerCategory.Loan, $"Kreditrückzahlung {loan.Lender}"))
            {
                return (false, "Nicht genug Guthaben für die Rückzahlung.");
            }

            loan.RemainingPrincipal -= amount;
            if (loan.RemainingPrincipal <= 0.01m) _state.Loans.Remove(loan);
            _state.SaveLoans();
        }

        _ = _persistence.SaveAsync();
        return (true, "");
    }

    public (bool Ok, string Error) DeclareBankruptcy()
    {
        lock (_state.Sync)
        {
            _state.ResetAfterBankruptcy();
            _state.PostLedger(0m, LedgerCategory.Bankruptcy, "Insolvenz und Neustart");
            _state.SaveLoans();
        }

        _ = _persistence.SaveAsync();
        return (true, "");
    }

    public void ChargeMonthlyInterest()
    {
        lock (_state.Sync)
        {
            foreach (var loan in _state.Loans.ToList())
            {
                var interest = Math.Round(loan.RemainingPrincipal * loan.MonthlyInterestRate / 100m, 2);
                if (interest <= 0) continue;
                _state.PostLedger(-interest, LedgerCategory.Interest, $"Zinsen {loan.Lender}");
            }

            if (_state.Company.Balance <= -1_000_000m)
            {
                _state.Company.GameOver = true;
                _state.AddLog("GAME OVER: Das Unternehmen ist mit mehr als 1.000.000 € überschuldet.");
            }
        }
    }

    public object GetOverview()
    {
        lock (_state.Sync)
        {
            var bank = GetOffer(LoanType.Bank);
            var shark = GetOffer(LoanType.LoanShark);
            return new
            {
                CreditScore = _state.CreditScore,
                Loans = _state.Loans.Select(l => new
                {
                    l.Id, l.Lender, l.Principal, l.RemainingPrincipal,
                    l.MonthlyInterestRate,
                    MonthlyInterest = Math.Round(l.RemainingPrincipal * l.MonthlyInterestRate / 100m, 2)
                }),
                Bank = new { bank.Lender, bank.MaxAmount, bank.MonthlyRate, bank.Reason },
                LoanShark = new { shark.Lender, shark.MaxAmount, shark.MonthlyRate, shark.Reason }
            };
        }
    }

    private LoanOffer GetOffer(LoanType type)
    {
        if (type == LoanType.LoanShark)
        {
            return new LoanOffer("Private Geldhaie", 50_000m, 18.0m, "Teuer, aber ohne Bonitätsprüfung.");
        }

        var score = _state.CreditScore;
        if (score >= 700) return new LoanOffer("Hausbank", 100_000m, 3.5m, "Sehr gute Bonität.");
        if (score >= 550) return new LoanOffer("Hausbank", 40_000m, 6.0m, "Mittlere Bonität.");
        return new LoanOffer("Hausbank", 0m, 9.0m, "Bonität zu schwach für einen Bankkredit.");
    }

    private sealed record LoanOffer(string Lender, decimal MaxAmount, decimal MonthlyRate, string Reason);
}