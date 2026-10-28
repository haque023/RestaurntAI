using Microsoft.Extensions.DependencyInjection;
using RestaurantAI.Order.Application.Orders;

namespace RestaurantAI.Order.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddOrderApplication(this IServiceCollection services)
    {
        services.AddScoped<OrderService>();
        return services;
    }
}
