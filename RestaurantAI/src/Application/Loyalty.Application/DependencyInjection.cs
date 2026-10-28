using Microsoft.Extensions.DependencyInjection;
using RestaurantAI.Loyalty.Application.Loyalty;

namespace RestaurantAI.Loyalty.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddLoyaltyApplication(this IServiceCollection services)
    {
        services.AddScoped<LoyaltyService>();
        return services;
    }
}
