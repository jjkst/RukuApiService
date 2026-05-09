namespace RukuServiceApi.Models;

public static class AuthorizationPolicies
{
    public const string AdminOnly = "AdminOnly";
    public const string AdminOrOwner = "AdminOrOwner";
    public const string AuthenticatedUser = "AuthenticatedUser";
}
