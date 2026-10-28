using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RestaurantAI.Infrastructure.Messaging.RabbitMQ;

namespace RestaurantAI.Infrastructure.Messaging.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public bool Enabled { get; set; } = true;

    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(2);

    public int BatchSize { get; set; } = 50;

    public int MaxAttempts { get; set; } = 10;
}

/// <summary>
/// Polls a module's outbox table and publishes pending messages to RabbitMQ.
/// Consumers must be idempotent: delivery is at-least-once.
/// </summary>
public sealed class OutboxDispatcher<TContext>(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxOptions> options,
    TimeProvider timeProvider,
    ILogger<OutboxDispatcher<TContext>> logger) : BackgroundService
    where TContext : DbContext
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(settings.PollingInterval, timeProvider);
        do
        {
            try
            {
                while (await DispatchBatchAsync(settings, stoppingToken) == settings.BatchSize)
                {
                    // Drain backlog before waiting for the next tick.
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Outbox dispatch for {Context} failed", typeof(TContext).Name);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task<int> DispatchBatchAsync(OutboxOptions settings, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        var repository = new OutboxRepository<TContext>(dbContext);
        var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

        // The retrying execution strategy only allows explicit transactions inside ExecuteAsync;
        // a retry re-runs the whole batch (delivery is at-least-once anyway).
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(
            async ct =>
            {
                dbContext.ChangeTracker.Clear();
                await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
                var batch = await repository.LockPendingBatchAsync(settings.BatchSize, settings.MaxAttempts, ct);

                foreach (var message in batch)
                {
                    try
                    {
                        await publisher.PublishAsync(
                            new IntegrationMessage(message.Id, message.Type, message.Payload, message.TenantId, message.CorrelationId, message.OccurredAt),
                            ct);
                        message.MarkProcessed(timeProvider.GetUtcNow());
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogWarning(ex, "Publishing outbox message {MessageId} ({Type}) failed", message.Id, message.Type);
                        message.MarkFailed(ex.Message);
                    }
                }

                await repository.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return batch.Count;
            },
            cancellationToken);
    }
}
