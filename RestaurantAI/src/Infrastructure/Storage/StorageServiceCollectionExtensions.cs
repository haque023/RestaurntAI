using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RestaurantAI.Infrastructure.Storage.ObjectStorage;

namespace RestaurantAI.Infrastructure.Storage;

public static class StorageServiceCollectionExtensions
{
    public const string ConnectionStringName = "ObjectStorage";

    public static IServiceCollection AddObjectStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");

        services.AddSingleton(new BlobServiceClient(connectionString));
        services.AddSingleton<IObjectStorage, BlobObjectStorage>();
        return services;
    }
}
