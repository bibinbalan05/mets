using System;
using System.Threading.Tasks;
using Mets.Replenishment.Core.Interfaces;

namespace Mets.Replenishment.Infrastructure.Services;

public class MockStockValidationService : IStockValidationService
{
    private static readonly Random _random = new Random();

    public async Task<int> GetAvailableStockAsync(string articleNumber)
    {
        // Simulate a slow external service (3-6 seconds delay)
        int delay = _random.Next(3000, 6000);
        await Task.Delay(delay);

        // Return a random mock stock availability (0 to 1000)
        return _random.Next(0, 1000);
    }
}
