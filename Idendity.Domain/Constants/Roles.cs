namespace Idendity.Domain.Constants;

/// <summary>
/// Predefined roles for the e-commerce system
/// </summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Customer = "Customer";
    public const string Seller = "Seller";
    public const string Support = "Support";
    
    public static readonly string[] All = { Admin, Customer, Seller, Support };
}

/// <summary>
/// Predefined policies for authorization
/// </summary>
public static class Policies
{
    public const string RequireAdmin = "RequireAdmin";
    public const string RequireSeller = "RequireSeller";
    public const string RequireSupport = "RequireSupport";
}


