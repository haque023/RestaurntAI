using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.ServiceDefaults.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Authentication:Jwt";

    /// <summary>OIDC authority (e.g. Entra ID / Entra External ID). Preferred outside Development.</summary>
    public string? Authority { get; set; }

    public string Issuer { get; set; } = "restaurant-ai";

    public string Audience { get; set; } = "restaurant-ai-api";

    /// <summary>Symmetric signing key used only for local development tokens issued by Identity.Api.</summary>
    public string? SigningKey { get; set; }
}

public static class PlatformAuthentication
{
    public static IServiceCollection AddPlatformJwtBearer(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var options = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwt =>
            {
                jwt.MapInboundClaims = false;
                jwt.RequireHttpsMetadata = !environment.IsDevelopment();

                if (!string.IsNullOrWhiteSpace(options.Authority))
                {
                    jwt.Authority = options.Authority;
                }

                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = options.Issuer,
                    ValidAudience = options.Audience,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    NameClaimType = PlatformClaims.Subject,
                    RoleClaimType = PlatformClaims.Role,
                    IssuerSigningKey = string.IsNullOrWhiteSpace(options.SigningKey)
                        ? null
                        : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
                };
            });

        return services;
    }
}
