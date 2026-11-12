using RestaurantAI.SharedKernel.Domain;

namespace RestaurantAI.Restaurant.Api.Models;

public enum RestaurantStatus
{
    Draft,
    PendingVerification,
    Active,
    Suspended,
}

/// <summary>
/// The tenant root. A restaurant's id is the tenant id used across the whole platform.
/// Lifecycle: PendingVerification (self sign-up) → Draft (verified, not listed) ⇄ Active
/// (listed in discovery); a platform administrator can Suspend at any point and Reinstate to Draft.
/// </summary>
public sealed class Restaurant : AggregateRoot<Guid>
{
    public const int MaxTags = 20;

    private readonly List<Branch> _branches = [];

    private Restaurant()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public List<string> Cuisines { get; private set; } = [];

    /// <summary>Dietary options the restaurant caters for (e.g. vegan, halal, gluten-free).</summary>
    public List<string> DietaryOptions { get; private set; } = [];

    public string DefaultCurrency { get; private set; } = "USD";

    /// <summary>IANA time zone (e.g. Asia/Dhaka) used to evaluate opening hours.</summary>
    public string TimeZone { get; private set; } = "UTC";

    public RestaurantStatus Status { get; private set; }

    public bool IsListed => Status == RestaurantStatus.Active;

    public string? Phone { get; private set; }

    public string? Website { get; private set; }

    public string PlanCode { get; private set; } = SubscriptionPlans.Starter.Code;

    /// <summary>Platform AI governance switch; off disables every AI agent for this restaurant.</summary>
    public bool AiEnabled { get; private set; } = true;

    /// <summary>Owner-maintained knowledge for the AI waiter (policies, FAQs, services). Never prices.</summary>
    public string AiKnowledge { get; private set; } = string.Empty;

    /// <summary>How the AI waiter should sound, e.g. "warm and brief".</summary>
    public string AiTone { get; private set; } = string.Empty;

    public string? SuspensionReason { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public SubscriptionPlan Plan => SubscriptionPlans.Find(PlanCode) ?? SubscriptionPlans.Starter;

    public IReadOnlyCollection<Branch> Branches => _branches.AsReadOnly();

    public const int MaxAiKnowledgeLength = 8000;

    /// <summary>Self-service sign-ups wait for verification; administrator-created restaurants start verified.</summary>
    public static Restaurant Register(string name, string defaultCurrency, string timeZone, bool requiresVerification = false, DateTimeOffset? now = null)
    {
        var restaurant = new Restaurant
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            DefaultCurrency = defaultCurrency,
            TimeZone = timeZone,
            Status = requiresVerification ? RestaurantStatus.PendingVerification : RestaurantStatus.Draft,
            CreatedAt = now ?? DateTimeOffset.UtcNow,
        };
        restaurant.Raise(new RestaurantRegistered(restaurant.Id, name, requiresVerification));
        return restaurant;
    }

    public void UpdateContact(string? phone, string? website)
    {
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim()[..Math.Min(phone.Trim().Length, 40)];
        Website = string.IsNullOrWhiteSpace(website) ? null : website.Trim()[..Math.Min(website.Trim().Length, 300)];
    }

    public void UpdateAiSettings(string knowledge, string tone)
    {
        if (knowledge.Length > MaxAiKnowledgeLength)
        {
            throw new DomainException("restaurant.ai_knowledge_too_long", $"AI knowledge can be at most {MaxAiKnowledgeLength} characters.");
        }

        AiKnowledge = knowledge.Trim();
        AiTone = tone.Trim()[..Math.Min(tone.Trim().Length, 200)];
    }

    /// <summary>Platform administrator verified the business; the owner can now list it.</summary>
    public void Approve()
    {
        if (Status != RestaurantStatus.PendingVerification)
        {
            throw new DomainException("restaurant.not_pending", $"Only restaurants awaiting verification can be approved (status is {Status}).");
        }

        Status = RestaurantStatus.Draft;
        Raise(new RestaurantVerified(Id));
    }

    public void Suspend(string reason)
    {
        if (Status == RestaurantStatus.Suspended)
        {
            return;
        }

        Status = RestaurantStatus.Suspended;
        SuspensionReason = string.IsNullOrWhiteSpace(reason) ? "Suspended by platform administration." : reason.Trim();
        Raise(new RestaurantSuspended(Id, SuspensionReason));
    }

    public void Reinstate()
    {
        if (Status != RestaurantStatus.Suspended)
        {
            throw new DomainException("restaurant.not_suspended", "Only suspended restaurants can be reinstated.");
        }

        Status = RestaurantStatus.Draft;
        SuspensionReason = null;
        Raise(new RestaurantReinstated(Id));
    }

    public void ChangePlan(string planCode)
    {
        var plan = SubscriptionPlans.Find(planCode)
            ?? throw new DomainException("restaurant.unknown_plan", $"Unknown plan '{planCode}'.");
        if (_branches.Count > plan.MaxBranches)
        {
            throw new DomainException("restaurant.plan_too_small", $"The {plan.Name} plan allows {plan.MaxBranches} branch(es); this restaurant has {_branches.Count}.");
        }

        if (PlanCode != plan.Code)
        {
            PlanCode = plan.Code;
            Raise(new RestaurantPlanChanged(Id, plan.Code));
        }
    }

    public void SetAiEnabled(bool enabled)
    {
        if (AiEnabled != enabled)
        {
            AiEnabled = enabled;
            Raise(new RestaurantAiToggled(Id, enabled));
        }
    }

    public void UpdateProfile(string description, IEnumerable<string> cuisines, IEnumerable<string> dietaryOptions)
    {
        Description = description.Trim();
        Cuisines = NormalizeTags(cuisines);
        DietaryOptions = NormalizeTags(dietaryOptions);
    }

    public Branch AddBranch(string name, string address)
    {
        if (_branches.Count >= Plan.MaxBranches)
        {
            throw new DomainException("restaurant.branch_limit", $"The {Plan.Name} plan allows {Plan.MaxBranches} branch(es). Upgrade to add more.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("branch.name_required", "Branch name is required.");
        }

        var branch = new Branch(Guid.CreateVersion7(), Id, name, address);
        _branches.Add(branch);
        return branch;
    }

    public Branch GetBranch(Guid branchId) =>
        _branches.SingleOrDefault(b => b.Id == branchId)
        ?? throw new DomainException("restaurant.branch_not_found", "Branch not found.");

    /// <summary>Lists the restaurant in consumer discovery.</summary>
    public void Activate()
    {
        if (_branches.Count == 0)
        {
            throw new DomainException("restaurant.no_branches", "A restaurant needs at least one branch before it can be listed.");
        }

        if (Status == RestaurantStatus.Suspended)
        {
            throw new DomainException("restaurant.suspended", "A suspended restaurant cannot be listed.");
        }

        if (Status == RestaurantStatus.PendingVerification)
        {
            throw new DomainException("restaurant.pending_verification", "The platform team is still verifying this restaurant. You can list it once it is approved.");
        }

        if (Status != RestaurantStatus.Active)
        {
            Status = RestaurantStatus.Active;
            Raise(new RestaurantActivated(Id));
        }
    }

    /// <summary>Removes the restaurant from consumer discovery without deleting anything.</summary>
    public void Unlist()
    {
        if (Status == RestaurantStatus.Active)
        {
            Status = RestaurantStatus.Draft;
        }
    }

    internal static List<string> NormalizeTags(IEnumerable<string> tags) =>
        tags.Select(t => t.Trim().ToLowerInvariant())
            .Where(t => t.Length is > 0 and <= 40)
            .Distinct()
            .Take(MaxTags)
            .ToList();
}

public sealed class Branch : Entity<Guid>, ITenantOwned
{
    private Branch()
    {
    }

    internal Branch(Guid id, Guid tenantId, string name, string address)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        Address = address;
    }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Address { get; private set; } = string.Empty;

    public double? Latitude { get; private set; }

    public double? Longitude { get; private set; }

    public List<OpeningPeriod> OpeningHours { get; private set; } = [];

    public void UpdateDetails(string name, string address, double? latitude, double? longitude)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("branch.name_required", "Branch name is required.");
        }

        if (latitude.HasValue != longitude.HasValue)
        {
            throw new DomainException("branch.invalid_location", "Provide both latitude and longitude, or neither.");
        }

        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            throw new DomainException("branch.invalid_location", "Latitude must be between -90 and 90 and longitude between -180 and 180.");
        }

        Name = name.Trim();
        Address = address.Trim();
        Latitude = latitude;
        Longitude = longitude;
    }

    public void SetOpeningHours(IEnumerable<OpeningPeriod> periods)
    {
        var list = periods.ToList();
        if (list.Any(p => p.Opens == p.Closes))
        {
            throw new DomainException("branch.invalid_hours", "Opening and closing times must differ.");
        }

        if (list.Count > 21)
        {
            throw new DomainException("branch.invalid_hours", "At most 3 opening periods per day are supported.");
        }

        OpeningHours = list.OrderBy(p => p.Day).ThenBy(p => p.Opens).ToList();
    }
}

public sealed record RestaurantActivated(Guid TenantId) : DomainEvent;

public sealed record RestaurantRegistered(Guid TenantId, string Name, bool RequiresVerification) : DomainEvent;

public sealed record RestaurantVerified(Guid TenantId) : DomainEvent;

public sealed record RestaurantSuspended(Guid TenantId, string Reason) : DomainEvent;

public sealed record RestaurantReinstated(Guid TenantId) : DomainEvent;

public sealed record RestaurantPlanChanged(Guid TenantId, string PlanCode) : DomainEvent;

public sealed record RestaurantAiToggled(Guid TenantId, bool Enabled) : DomainEvent;
