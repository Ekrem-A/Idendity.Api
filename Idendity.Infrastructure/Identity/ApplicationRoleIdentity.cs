using Microsoft.AspNetCore.Identity;

namespace Idendity.Infrastructure.Identity;

/// <summary>
/// ASP.NET Identity Role with custom properties
/// </summary>
public class ApplicationRoleIdentity : IdentityRole<Guid>
{
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public virtual ICollection<ApplicationUserRoleIdentity> UserRoles { get; set; } = new List<ApplicationUserRoleIdentity>();

    public ApplicationRoleIdentity() : base()
    {
    }

    public ApplicationRoleIdentity(string roleName) : base(roleName)
    {
        Id = Guid.NewGuid();
    }
}


