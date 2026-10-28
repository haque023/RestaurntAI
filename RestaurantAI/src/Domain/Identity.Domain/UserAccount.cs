using RestaurantAI.SharedKernel.Domain;

namespace RestaurantAI.Identity.Domain;

/// <summary>
/// A platform identity. Credentials are owned by the external identity provider; this record
/// links the subject to tenant memberships, roles and permissions.
/// </summary>
public sealed class UserAccount : AggregateRoot<Guid>
{
    private readonly List<TenantMembership> _memberships = [];

    private UserAccount()
    {
    }

    public string ExternalSubject { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public bool IsPlatformAdministrator { get; private set; }

    public IReadOnlyCollection<TenantMembership> Memberships => _memberships.AsReadOnly();

    public static UserAccount Register(string externalSubject, string email, string displayName)
    {
        var user = new UserAccount
        {
            Id = Guid.CreateVersion7(),
            ExternalSubject = externalSubject,
            Email = email.Trim().ToLowerInvariant(),
            DisplayName = displayName,
        };
        user.Raise(new UserRegistered(user.Id, user.Email));
        return user;
    }

    public void GrantMembership(Guid tenantId, Guid? branchId, string role)
    {
        if (_memberships.Any(m => m.TenantId == tenantId && m.BranchId == branchId && m.Role == role))
        {
            return;
        }

        _memberships.Add(new TenantMembership(tenantId, branchId, role));
        Raise(new MembershipGranted(Id, tenantId, branchId, role));
    }
}

public sealed record TenantMembership(Guid TenantId, Guid? BranchId, string Role);

public sealed record UserRegistered(Guid UserId, string Email) : DomainEvent;

public sealed record MembershipGranted(Guid UserId, Guid TenantId, Guid? BranchId, string Role) : DomainEvent;
