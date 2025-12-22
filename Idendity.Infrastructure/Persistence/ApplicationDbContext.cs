using Idendity.Domain.Entities;
using Idendity.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Idendity.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<ApplicationUserIdentity, ApplicationRoleIdentity, Guid,
    IdentityUserClaim<Guid>, ApplicationUserRoleIdentity, IdentityUserLogin<Guid>,
    IdentityRoleClaim<Guid>, IdentityUserToken<Guid>>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Rename Identity tables
        builder.Entity<ApplicationUserIdentity>().ToTable("Users");
        builder.Entity<ApplicationRoleIdentity>().ToTable("Roles");
        builder.Entity<ApplicationUserRoleIdentity>().ToTable("UserRoles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");

        // Configure RefreshToken
        builder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("RefreshTokens");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Token).HasMaxLength(500).IsRequired();
            entity.Property(e => e.DeviceInfo).HasMaxLength(500);
            entity.Property(e => e.IpAddress).HasMaxLength(50);
            entity.Property(e => e.ReplacedByToken).HasMaxLength(500);
            entity.Property(e => e.ReasonRevoked).HasMaxLength(200);
            
            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasIndex(e => e.UserId);
            
            entity.HasOne<ApplicationUserIdentity>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.Ignore(e => e.User);
        });

        // Configure ApplicationUser additional properties
        builder.Entity<ApplicationUserIdentity>(entity =>
        {
            entity.Property(e => e.FirstName).HasMaxLength(50).IsRequired();
            entity.Property(e => e.LastName).HasMaxLength(50).IsRequired();
        });

        // Configure UserRoles
        builder.Entity<ApplicationUserRoleIdentity>(entity =>
        {
            entity.HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId)
                .IsRequired();

            entity.HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId)
                .IsRequired();
        });
    }
}
