using System.Globalization;
using Microsoft.Net.Http.Headers;

namespace RestaurantAI.AI.Api.Services;

/// <summary>Request checks shared by the AI assistants.</summary>
internal static class AiRequest
{
    public const int MaxMessageLength = 2000;

    internal static IResult? ValidateMessage(string? message) =>
        string.IsNullOrWhiteSpace(message) || message.Length > MaxMessageLength
            ? Problem("ai.invalid_message", $"Message must be 1-{MaxMessageLength} characters.", StatusCodes.Status400BadRequest)
            : null;

    /// <summary>The agent calls tools with the caller's own token, so it can never exceed their permissions.</summary>
    internal static string BearerToken(HttpContext httpContext)
    {
        var authorization = httpContext.Request.Headers[HeaderNames.Authorization].ToString();
        return authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? authorization["Bearer ".Length..].Trim() : string.Empty;
    }

    internal static IResult Problem(string title, string detail, int statusCode) =>
        TypedResults.Problem(title: title, detail: detail, statusCode: statusCode);
}
