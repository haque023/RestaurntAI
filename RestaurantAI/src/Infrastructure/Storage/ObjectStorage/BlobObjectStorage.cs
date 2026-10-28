using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace RestaurantAI.Infrastructure.Storage.ObjectStorage;

/// <summary>Azure Blob Storage implementation (Azurite locally).</summary>
internal sealed class BlobObjectStorage(BlobServiceClient client) : IObjectStorage
{
    public async Task<StoredObject> UploadAsync(
        Guid tenantId,
        string container,
        string name,
        Stream content,
        string contentType,
        CancellationToken cancellationToken)
    {
        var containerClient = client.GetBlobContainerClient(container);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

        var key = $"{tenantId:N}/{Guid.CreateVersion7():N}/{Path.GetFileName(name)}";
        var blob = containerClient.GetBlobClient(key);
        await blob.UploadAsync(content, new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } }, cancellationToken);

        var properties = await blob.GetPropertiesAsync(cancellationToken: cancellationToken);
        return new StoredObject(container, key, contentType, properties.Value.ContentLength);
    }

    public async Task<Stream?> OpenReadAsync(string container, string key, CancellationToken cancellationToken)
    {
        var blob = client.GetBlobContainerClient(container).GetBlobClient(key);
        return await blob.ExistsAsync(cancellationToken) ? await blob.OpenReadAsync(cancellationToken: cancellationToken) : null;
    }

    public Task DeleteAsync(string container, string key, CancellationToken cancellationToken) =>
        client.GetBlobContainerClient(container).GetBlobClient(key).DeleteIfExistsAsync(cancellationToken: cancellationToken);

    public Uri GetReadUri(string container, string key, TimeSpan validFor)
    {
        var blob = client.GetBlobContainerClient(container).GetBlobClient(key);
        return blob.CanGenerateSasUri
            ? blob.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.Add(validFor))
            : blob.Uri;
    }
}
