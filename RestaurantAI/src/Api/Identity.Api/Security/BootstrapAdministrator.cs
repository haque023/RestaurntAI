using Microsoft.EntityFrameworkCore;
using RestaurantAI.Identity.Api.Persistence;
using RestaurantAI.Identity.Domain;

namespace RestaurantAI.Identity.Api.Security;

public sealed class BootstrapAdministratorOptions
{
    public const string SectionName = "Identity:BootstrapAdmin";

    public string? Email { get; set; }

    public string? Password { get; set; }

    public string DisplayName { get; set; } = "Platform Admin";
}

/// <summary>
/// Creates the first platform administrator from configuration when none exists, so a fresh
/// environment can be administered. Supply the password through a secret store, never source.
/// </summary>
public static class BootstrapAdministrator
{
    public static async Task EnsureAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var options = configuration.GetSection(BootstrapAdministratorOptions.SectionName).Get<BootstrapAdministratorOptions>();
        if (string.IsNullOrWhiteSpace(options?.Email) || string.IsNullOrWhiteSpace(options.Password))
        {
            return;
        }

        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        if (await dbContext.Users.AnyAsync(u => u.IsPlatformAdministrator, cancellationToken))
        {
            return;
        }

        var hasher = scope.ServiceProvider.GetRequiredService<PasswordHasher>();
        var email = UserAccount.NormalizeEmail(options.Email);
        var user = await dbContext.Users.SingleOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (user is null)
        {
            user = UserAccount.Register(email, options.DisplayName, hasher.Hash(options.Password), scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow());
            dbContext.Users.Add(user);
        }

        user.PromoteToPlatformAdministrator();
        await dbContext.SaveChangesAsync(cancellationToken);
        scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(BootstrapAdministrator))
            .LogInformation("Bootstrap platform administrator {Email} is ready.", email);
    }
}
