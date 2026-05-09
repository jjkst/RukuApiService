namespace RukuServiceApi.Models;

public class CreateServiceRequest
{
    public string Title { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<string>? Features { get; set; }
    public List<PricingPlan>? PricingPlans { get; set; }
}

public class UpdateServiceRequest
{
    public string Title { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<string>? Features { get; set; }
    public List<PricingPlan>? PricingPlans { get; set; }
}

public class CreateUserRequest
{
    public string Email { get; set; } = string.Empty;
    public string Uid { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public bool EmailVerified { get; set; }
    public ProviderList Provider { get; set; }
}

public class UpdateUserRoleRequest
{
    public string Role { get; set; } = string.Empty;
}

public class ContactRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Questions { get; set; } = string.Empty;
}
