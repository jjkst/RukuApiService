using System.ComponentModel.DataAnnotations;

namespace RukuServiceApi.Models;

public class User
{
    [Key]
    public int Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool EmailVerified { get; set; }
    public string Uid { get; set; } = string.Empty;

    public UserRole Role { get; set; }
    public ProviderList Provider { get; set; }
}

public enum UserRole
{
    Admin = 1,
    Owner = 2,
    Subscriber = 3,
}

public enum ProviderList
{
    Google = 1,
    Facebook = 2,
    Apple = 3,
    GitHub = 4,
    Email = 5,
}
