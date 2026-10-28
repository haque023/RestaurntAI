using RestaurantAI.SharedKernel.Domain;

namespace RestaurantAI.Restaurant.Domain;

public enum RestaurantStatus
{
    Draft,
    PendingVerification,
    Active,
    Suspended,
}

/// <summary>
/// The tenant root. A restaurant's id is the tenant id used across the whole platform.
/// Only <see cref="RestaurantStatus.Active"/> restaurants are listed in consumer discovery.
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

    public IReadOnlyCollection<Branch> Branches => _branches.AsReadOnly();

    public static Restaurant Register(string name, string defaultCurrency, string timeZone) => new()
    {
        Id = Guid.CreateVersion7(),
        Name = name,
        DefaultCurrency = defaultCurrency,
        TimeZone = timeZone,
        Status = RestaurantStatus.Draft,
    };

    public void UpdateProfile(string description, IEnumerable<string> cuisines, IEnumerable<string> dietaryOptions)
    {
        Description = description.Trim();
        Cuisines = NormalizeTags(cuisines);
        DietaryOptions = NormalizeTags(dietaryOptions);
    }

    public Branch AddBranch(string name, string address)
    {
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
