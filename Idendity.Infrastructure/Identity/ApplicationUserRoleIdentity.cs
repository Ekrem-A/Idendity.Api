using Microsoft.AspNetCore.Identity;

namespace Idendity.Infrastructure.Identity;

/// <summary>
/// ASP.NET Identity User-Role join table
/// </summary>
public class ApplicationUserRoleIdentity : IdentityUserRole<Guid>
{
    public virtual ApplicationUserIdentity User { get; set; } = null!;
    public virtual ApplicationRoleIdentity Role { get; set; } = null!;
}


