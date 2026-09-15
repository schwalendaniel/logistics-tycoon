namespace LogisticsGame.Api.Models;

public record DispatchTourRequest(Guid JobId, Guid TruckId, Guid DriverId);

public record RefuelRequest(Guid TruckId);

public record MaintainRequest(Guid TruckId, MaintenanceLevel Level);

public record BailRequest(Guid TruckId);

public record SellTruckRequest(Guid TruckId);

public record BuyTruckRequest(string CatalogId);

public record HireDriverRequest(Guid ProspectId);

public record AdjustSalaryRequest(Guid DriverId, decimal MonthlySalary);

public record BonusRequest(Guid DriverId, decimal Amount);

public record DismissDriverRequest(Guid DriverId);

public record BuyDepotRequest(string CityName);

public record DriverIdRequest(Guid DriverId);

public record SetGameSpeedRequest(double SpeedMultiplier);

public record BorrowRequest(LoanType Type, decimal Amount);

public record RepayLoanRequest(Guid LoanId, decimal Amount);

public record RecoverCargoRequest(Guid? TourId, Guid BrokenDownTruckId, Guid RescueTruckId, Guid RescueDriverId);

public record TowTruckRequest(Guid? TourId, Guid BrokenDownTruckId);
