using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RestaurantAI.Mcp.Gateway.Context;

namespace RestaurantAI.Mcp.Gateway.Idempotency;

/// <summary>
/// Derives a deterministic idempotency key for a mutating tool call from
/// tenant + user + tool + conversation turn + canonical arguments.
/// The same call retried within one turn maps to the same key; the same request in a new turn
/// (a deliberate second order) gets a new key. An explicit key from the agent always wins.
/// </summary>
public sealed class IdempotencyKeyGenerator
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public string Generate(string toolName, ToolCallerContext caller, object arguments, string? explicitKey = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitKey))
        {
            return $"mcp:{Hash($"{caller.Restaurant.TenantId}|{caller.User.UserId}|{toolName}|{explicitKey}")[..40]}";
        }

        var material = string.Join(
            '|',
            caller.Restaurant.TenantId,
            caller.User.UserId,
            toolName,
            caller.ConversationId ?? "-",
            caller.TurnId ?? "-",
            HashArguments(arguments));

        return $"mcp:{Hash(material)[..40]}";
    }

    public static string HashArguments(object arguments) =>
        Hash(JsonSerializer.Serialize(arguments, arguments.GetType(), SerializerOptions));

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
