using Microsoft.Extensions.DependencyInjection;

namespace RestaurantAI.Consumer.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddConsumerApplication(this IServiceCollection services)
    {
        // Register Consumer use cases here.
        return services;
    }
}
