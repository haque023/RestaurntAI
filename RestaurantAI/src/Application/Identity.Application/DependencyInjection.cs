using Microsoft.Extensions.DependencyInjection;

namespace RestaurantAI.Identity.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        // Register Identity use cases here.
        return services;
    }
}
