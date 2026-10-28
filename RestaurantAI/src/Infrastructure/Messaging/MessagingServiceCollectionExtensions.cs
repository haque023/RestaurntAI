using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RestaurantAI.Infrastructure.Messaging.Outbox;
using RestaurantAI.Infrastructure.Messaging.RabbitMQ;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Infrastructure.Messaging;

public static class MessagingServiceCollectionExtensions
{
    public static IServiceCollection AddRabbitMq(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();
        return services;
    }

    /// <summary>
    /// Enables the transactional outbox for a module DbContext: domain events are captured on
    /// SaveChanges and a background dispatcher publishes them.
    /// Add <see cref="DomainEventOutboxInterceptor"/> to the DbContext via <see cref="UseOutbox"/>.
    /// </summary>
    public static IServiceCollection AddOutbox<TContext>(this IServiceCollection services, IConfiguration configuration, string module)
        where TContext : DbContext
    {
        services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));
        services.AddScoped(provider => new DomainEventOutboxInterceptor(
            module,
            provider.GetRequiredService<ITenantContext>(),
            provider.GetRequiredService<ICorrelationContext>()));
        services.AddScoped<OutboxRepository<TContext>>();
        services.AddHostedService<OutboxDispatcher<TContext>>();
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }

    public static DbContextOptionsBuilder UseOutbox(this DbContextOptionsBuilder options, IServiceProvider provider) =>
        options.AddInterceptors(provider.GetRequiredService<DomainEventOutboxInterceptor>());
}
