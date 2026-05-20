using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Mets.Replenishment.Core.Enums;
using Mets.Replenishment.Core.Interfaces;
using Mets.Replenishment.Infrastructure.Data;

namespace Mets.Replenishment.Api.Background;

public class StockValidationBackgroundService : BackgroundService
{
    private readonly IValidationJobQueue _queue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StockValidationBackgroundService> _logger;

    public StockValidationBackgroundService(
        IValidationJobQueue queue,
        IServiceProvider serviceProvider,
        ILogger<StockValidationBackgroundService> logger)
    {
        _queue = queue;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var requestId = await _queue.DequeueAsync(stoppingToken);
                await ProcessValidationJobAsync(requestId, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Prevent throwing if stopping token is cancelled
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing validation job");
            }
        }
    }

    private async Task ProcessValidationJobAsync(Guid requestId, CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReplenishmentDbContext>();
        var stockService = scope.ServiceProvider.GetRequiredService<IStockValidationService>();

        var request = await dbContext.Requests
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == requestId, stoppingToken);

        if (request == null) return;

        foreach (var item in request.Items)
        {
            try
            {
                item.StockAvailable = await stockService.GetAvailableStockAsync(item.ArticleNumber);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get stock for item {ArticleNumber}", item.ArticleNumber);
                request.ValidationStatus = ValidationStatus.Failed;
                await dbContext.SaveChangesAsync(stoppingToken);
                return;
            }
        }

        request.ValidationStatus = ValidationStatus.Completed;
        await dbContext.SaveChangesAsync(stoppingToken);
        _logger.LogInformation("Completed validation for request {RequestId}", requestId);
    }
}
