using RestaurantAI.SharedKernel.Domain;

namespace RestaurantAI.Identity.Domain;

/// <summary>
/// A platform identity with a local password credential, linked to tenant memberships (role +
/// optional branch). An external identity provider can replace the credential later; the
/// memberships stay the source for roles and permissions.
/// </summary>
public sealed class UserAccount : AggregateRoot<Guid>
{
    public const int MaxFailedLogins = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);

    private readonly List<TenantMembership> _memberships = [];

    private UserAccount()
    {
    }

    public string ExternalSubject { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public string? PasswordHash { get; private set; }

    public bool IsPlatformAdministrator { get; private set; }

    public bool IsActive { get; private set; } = true;

    public int FailedLogins { get; private set; }

    public DateTimeOffset? LockedUntil { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? LastLoginAt { get; private set; }

    public IReadOnlyCollection<TenantMembership> Memberships => _memberships.AsReadOnly();

    public static UserAccount Register(string email, string displayName, string passwordHash, DateTimeOffset now)
    {
        var normalized = NormalizeEmail(email);
        if (normalized.Length is < 3 or > 320 || !normalized.Contains('@'))
        {
            throw new DomainException("user.invalid_email", "A valid email address is required.");
        }

        var id = Guid.CreateVersion7();
        var user = new UserAccount
        {
            Id = id,
            ExternalSubject = $"local:{id}",
            Email = normalized,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? normalized.Split('@')[0] : displayName.Trim(),
            PasswordHash = passwordHash,
            CreatedAt = now,
        };
        user.Raise(new UserRegistered(user.Id, user.Email));
        return user;
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public bool IsLockedOut(DateTimeOffset now) => LockedUntil is { } until && until > now;

    public void RecordSuccessfulLogin(DateTimeOffset now)
    {
        FailedLogins = 0;
        LockedUntil = null;
        LastLoginAt = now;
    }

    public void RecordFailedLogin(DateTimeOffset now)
    {
        FailedLogins++;
        if (FailedLogins >= MaxFailedLogins)
        {
            LockedUntil = now.Add(LockoutDuration);
            FailedLogins = 0;
        }
    }

    public void ChangePassword(string passwordHash) => PasswordHash = passwordHash;

    public void PromoteToPlatformAdministrator() => IsPlatformAdministrator = true;

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    public void GrantMembership(Guid tenantId, string tenantName, Guid? branchId, string role)
    {
        if (_memberships.Any(m => m.TenantId == tenantId && m.BranchId == branchId && m.Role == role))
        {
            return;
        }

        _memberships.Add(new TenantMembership(tenantId, tenantName, branchId, role));
        Raise(new MembershipGranted(Id, tenantId, branchId, role));
    }

    /// <summary>Removes every membership the user holds in the tenant.</summary>
    public bool RevokeMemberships(Guid tenantId)
    {
        var removed = _memberships.RemoveAll(m => m.TenantId == tenantId);
        if (removed > 0)
        {
            Raise(new MembershipsRevoked(Id, tenantId));
        }

        return removed > 0;
    }
}

public sealed record TenantMembership(Guid TenantId, string TenantName, Guid? BranchId, string Role);

public sealed record UserRegistered(Guid UserId, string Email) : DomainEvent;

public sealed record MembershipGranted(Guid UserId, Guid TenantId, Guid? BranchId, string Role) : DomainEvent;

public sealed record MembershipsRevoked(Guid UserId, Guid TenantId) : DomainEvent;
