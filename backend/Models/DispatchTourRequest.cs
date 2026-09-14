namespace LogisticsGame.Api.Models;

public record DispatchTourRequest(Job Job, Guid TruckId, Guid DriverId);