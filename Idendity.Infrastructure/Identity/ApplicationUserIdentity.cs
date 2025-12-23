using Microsoft.AspNetCore.Identity;

namespace Idendity.Infrastructure.Identity;

/// <summary>
/// ASP.NET Identity User with custom properties
/// </summary>
public class ApplicationUserIdentity : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsActive { get; set; } = true;

    public string FullName => $"{FirstName} {LastName}".Trim();
    
    public virtual ICollection<ApplicationUserRoleIdentity> UserRoles { get; set; } = new List<ApplicationUserRoleIdentity>();
}


