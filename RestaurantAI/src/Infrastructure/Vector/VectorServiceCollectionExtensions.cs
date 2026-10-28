using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pgvector.EntityFrameworkCore;
using RestaurantAI.Infrastructure.Persistence.PostgreSQL;
using RestaurantAI.Infrastructure.Vector.PgVector;

namespace RestaurantAI.Infrastructure.Vector;

public static class VectorServiceCollectionExtensions
{
    public static IServiceCollection AddPgVectorStore(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddPostgresDbContext<VectorDbContext>(configuration, VectorDbContext.SchemaName, npgsql => npgsql.UseVector());
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IVectorStore, PgVectorStore>();
        return services;
    }
}
