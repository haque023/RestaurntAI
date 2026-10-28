using Microsoft.Extensions.DependencyInjection;

namespace RestaurantAI.Restaurant.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddRestaurantApplication(this IServiceCollection services)
    {
        // Register Restaurant use cases here.
        return services;
    }
}
