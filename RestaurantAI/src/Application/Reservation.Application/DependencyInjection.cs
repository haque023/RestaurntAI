using Microsoft.Extensions.DependencyInjection;
using RestaurantAI.Reservation.Application.Reservations;

namespace RestaurantAI.Reservation.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddReservationApplication(this IServiceCollection services)
    {
        services.AddScoped<ReservationService>();
        return services;
    }
}
