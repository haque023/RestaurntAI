namespace RestaurantAI.Infrastructure.Storage.ObjectStorage;

public sealed record StoredObject(string Container, string Key, string ContentType, long Size);

/// <summary>
/// Stores non-transactional binary objects (restaurant/menu images, knowledge documents,
/// generated reports). Database rows keep only the returned reference.
/// Object keys are always prefixed with the tenant id.
/// </summary>
public interface IObjectStorage
{
    Task<StoredObject> UploadAsync(Guid tenantId, string container, string name, Stream content, string contentType, CancellationToken cancellationToken);

    Task<Stream?> OpenReadAsync(string container, string key, CancellationToken cancellationToken);

    Task DeleteAsync(string container, string key, CancellationToken cancellationToken);

    Uri GetReadUri(string container, string key, TimeSpan validFor);
}
