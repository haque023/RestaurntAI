namespace RestaurantAI.SharedKernel.Tenancy;

/// <summary>
/// Provides the correlation id of the current unit of work (HTTP request or message).
/// </summary>
public interface ICorrelationContext
{
    string? CorrelationId { get; }
}
