using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RestaurantAI.ServiceDefaults.Authentication;
using RestaurantAI.ServiceDefaults.Correlation;
using RestaurantAI.ServiceDefaults.Errors;
using RestaurantAI.ServiceDefaults.Tenancy;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.ServiceDefaults;

/// <summary>
/// Cross-cutting defaults shared by every ASP.NET Core service in the platform:
/// observability, health checks, standardized errors, authentication and tenant context.
/// </summary>
public static class ServiceDefaultsExtensions
{
    public const string LivenessTag = "live";

    public static WebApplicationBuilder AddServiceDefaults(this WebApplicationBuilder builder)
    {
        builder.AddPlatformObservability();

        builder.Services.AddPlatformProblemDetails();

        // Enums travel as strings ("AiWaiter", "Confirmed") for web, mobile and MCP clients.
        builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.AddPlatformJwtBearer(builder.Configuration, builder.Environment);
        builder.Services.AddAuthorization();

        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ITenantContext, HttpTenantContext>();
        builder.Services.AddScoped<ICorrelationContext, HttpCorrelationContext>();

        return builder;
    }

    public static WebApplication UseServiceDefaults(this WebApplication app)
    {
        app.UseMiddleware<CorrelationContextMiddleware>();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseAuthentication();
        app.UseAuthorization();

        return app;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(LivenessTag),
        }).AllowAnonymous();

        app.MapHealthChecks("/health/ready").AllowAnonymous();

        return app;
    }

    /// <summary>OpenTelemetry logs/metrics/traces plus liveness health check. Used by every host, including gateways.</summary>
    public static WebApplicationBuilder AddPlatformObservability(this WebApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), [LivenessTag]);

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(builder.Environment.ApplicationName))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddMeter("System.Runtime"))
            .WithTracing(tracing => tracing
                .AddSource(builder.Environment.ApplicationName)
                .AddSource(PlatformActivitySources.Name)
                .AddAspNetCoreInstrumentation(options =>
                    options.Filter = context => !context.Request.Path.StartsWithSegments("/health"))
                .AddHttpClientInstrumentation());

        // Export via OTLP only when a collector is configured (OTEL_EXPORTER_OTLP_ENDPOINT).
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }
}

public static class PlatformActivitySources
{
    public const string Name = "RestaurantAI";
}
