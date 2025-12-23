namespace Idendity.Domain.Entities;

/// <summary>
/// Application role entity for authorization
/// </summary>
public class ApplicationRole
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string ConcurrencyStamp { get; set; } = Guid.NewGuid().ToString();
    
    // Custom properties
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual ICollection<ApplicationUserRole> UserRoles { get; set; } = new List<ApplicationUserRole>();
}

/// <summary>
/// Join table for User-Role many-to-many relationship
/// </summary>
public class ApplicationUserRole
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    
    public virtual ApplicationUser User { get; set; } = null!;
    public virtual ApplicationRole Role { get; set; } = null!;
}


