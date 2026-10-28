using Microsoft.Extensions.DependencyInjection;
using RestaurantAI.Menu.Application.Menu;

namespace RestaurantAI.Menu.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddMenuApplication(this IServiceCollection services)
    {
        services.AddScoped<MenuService>();
        return services;
    }
}
