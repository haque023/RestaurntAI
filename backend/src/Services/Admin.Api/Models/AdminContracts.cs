using System.Text.Json;

namespace RestaurantAI.Admin.Api.Models;

public sealed record AuditEventDto(Guid Id, string Type, string Module, Guid? TenantId, Guid? ActorId, string? CorrelationId, JsonElement Payload, DateTimeOffset OccurredAt);
