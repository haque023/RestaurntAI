using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using RestaurantAI.Order.Application.Abstractions;
using RestaurantAI.SharedKernel.Tenancy;
using Testcontainers.PostgreSql;

namespace RestaurantAI.Integration.Tests;

/// <summary>
/// Hosts Order.Api against a real PostgreSQL container. Migrations (including RLS policies)
/// run on startup. The Menu service is replaced by an in-memory catalog and the outbox
/// dispatcher is disabled so the test can inspect outbox rows directly.
/// </summary>
public sealed class OrderApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string SigningKey = "integration-test-signing-key-0123456789abcdef";

    // Non-superuser application role, so Row-Level Security is actually enforced.
    private const string InitSql = """
        CREATE ROLE restaurant_app LOGIN PASSWORD 'restaurant_app' NOSUPERUSER NOBYPASSRLS;
        GRANT ALL ON DATABASE restaurant_ai TO restaurant_app;
        GRANT ALL ON SCHEMA public TO restaurant_app;
        """;

    private PostgreSqlContainer? _postgres;

    public InMemoryMenuCatalog Catalog { get; } = new();

    public string? StartupError { get; private set; }

    public string AdminConnectionString => Postgres.GetConnectionString();

    private PostgreSqlContainer Postgres => _postgres ?? throw new InvalidOperationException(StartupError);

    public async ValueTask InitializeAsync()
    {
        try
        {
            // Building the container already probes Docker, so it must happen inside the guard.
            _postgres = new PostgreSqlBuilder("postgres:17-alpine")
                .WithDatabase("restaurant_ai")
                .WithResourceMapping(Encoding.UTF8.GetBytes(InitSql), "/docker-entrypoint-initdb.d/01-app-role.sql")
                .Build();
            await _postgres.StartAsync();
        }
        catch (Exception ex)
        {
            StartupError = $"Docker is required for integration tests: {ex.Message}";
        }
    }

    public new async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var appConnection = $"Host={Postgres.Hostname};Port={Postgres.GetMappedPublicPort(5432)};Database=restaurant_ai;Username=restaurant_app;Password=restaurant_app";

        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Platform", appConnection);
        builder.UseSetting("ConnectionStrings:Redis", string.Empty);
        builder.UseSetting("Authentication:Jwt:SigningKey", SigningKey);
        builder.UseSetting("Outbox:Enabled", "false");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IMenuCatalog>();
            services.AddSingleton<IMenuCatalog>(Catalog);
        });
    }

    public HttpClient CreateClientFor(Guid tenantId, Guid userId, params string[] permissions)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", CreateToken(tenantId, userId, permissions));
        return client;
    }

    private static string CreateToken(Guid tenantId, Guid userId, string[] permissions)
    {
        var claims = new List<Claim>
        {
            new(PlatformClaims.Subject, userId.ToString()),
            new(PlatformClaims.TenantId, tenantId.ToString()),
            new(PlatformClaims.Role, PlatformRoles.Customer),
        };
        claims.AddRange(permissions.Select(p => new Claim(PlatformClaims.Permission, p)));

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "restaurant-ai",
            Audience = "restaurant-ai-api",
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(10),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256),
        });
    }
}

public sealed class InMemoryMenuCatalog : IMenuCatalog
{
    private readonly Dictionary<Guid, CatalogItem> _items = [];

    public void Add(CatalogItem item) => _items[item.Id] = item;

    public Task<IReadOnlyDictionary<Guid, CatalogItem>> GetItemsAsync(Guid branchId, IReadOnlyCollection<Guid> menuItemIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, CatalogItem>>(
            menuItemIds.Where(_items.ContainsKey).ToDictionary(id => id, id => _items[id]));
}
