using System.Text.Json;
using EnterpriseAssetLifecycle.Domain;
using EnterpriseAssetLifecycle.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EnterpriseAssetLifecycle.Services;

public sealed class WarrantyMonitorOptions
{
    public int WarningDays { get; set; } = 30;
    public int IntervalMinutes { get; set; } = 1440;
}

public sealed class WarrantyMonitorService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IOptions<WarrantyMonitorOptions> options,
    ILogger<WarrantyMonitorService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Warranty expiration scan failed");
            }

            var minutes = Math.Clamp(options.Value.IntervalMinutes, 1, 10080);
            await Task.Delay(TimeSpan.FromMinutes(minutes), timeProvider, stoppingToken);
        }
    }

    internal async Task ScanAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AssetDbContext>();
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var deadline = today.AddDays(Math.Clamp(options.Value.WarningDays, 1, 365));
        var warranties = await db.Warranties
            .AsNoTracking()
            .Where(x => x.EndDate >= today && x.EndDate <= deadline)
            .ToListAsync(cancellationToken);

        foreach (var warranty in warranties)
        {
            var key = $"warranty-expiring:{warranty.Id}:{warranty.EndDate:yyyy-MM-dd}";
            if (await db.AssetEvents.AnyAsync(x => x.DeduplicationKey == key, cancellationToken))
            {
                continue;
            }

            db.AssetEvents.Add(new AssetEvent
            {
                AssetId = warranty.AssetId,
                Type = AssetEventType.WarrantyExpiring,
                Actor = "warranty-monitor",
                OccurredAt = timeProvider.GetUtcNow(),
                DeduplicationKey = key,
                Data = JsonSerializer.Serialize(new { warranty.Id, warranty.EndDate })
            });
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            logger.LogInformation(exception, "A concurrent warranty scan already emitted one or more notifications");
        }
    }
}

