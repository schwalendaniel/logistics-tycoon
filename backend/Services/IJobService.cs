using LogisticsGame.Api.Models;

namespace LogisticsGame.Api.Services;

public interface IJobService
{
    Task<List<Job>> GenerateAvailableJobsAsync(int count = 5);
}