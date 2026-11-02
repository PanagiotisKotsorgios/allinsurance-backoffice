using AllInsurance.Application.Abstractions;
using AllInsurance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AllInsurance.Infrastructure.Persistence;

public sealed class AllInsuranceDbContext(DbContextOptions<AllInsuranceDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RecoveryCode> RecoveryCodes => Set<RecoveryCode>();
    public DbSet<SecurityEvent> SecurityEvents => Set<SecurityEvent>();
    public DbSet<AppMetadata> AppMetadata => Set<AppMetadata>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Username).IsRequired().HasMaxLength(100);
            entity.Property(user => user.NormalizedUsername).IsRequired().HasMaxLength(100);
            entity.HasIndex(user => user.NormalizedUsername).IsUnique();
            entity.Property(user => user.DisplayName).IsRequired().HasMaxLength(200);
            entity.Property(user => user.PasswordHash).IsRequired();
            entity.Property(user => user.IsActive).HasDefaultValue(true);
            entity.Property(user => user.MustChangePassword).HasDefaultValue(false);
            entity.Property(user => user.FailedLoginCount).HasDefaultValue(0);
            entity.Property(user => user.CreatedAtUtc).IsRequired();
            entity.Property(user => user.UpdatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasKey(role => role.Id);
            entity.Property(role => role.Name).IsRequired().HasMaxLength(100);
            entity.Property(role => role.NormalizedName).IsRequired().HasMaxLength(100);
            entity.HasIndex(role => role.NormalizedName).IsUnique();
            entity.Property(role => role.Description).HasMaxLength(500);
            entity.HasData(
                new Role
                {
                    Id = Guid.Parse("a34f3189-90c9-4cec-9f7d-1a8cd4d19001"),
                    Name = "Administrator",
                    NormalizedName = "ADMINISTRATOR",
                    Description = "Full application administration"
                },
                new Role
                {
                    Id = Guid.Parse("a34f3189-90c9-4cec-9f7d-1a8cd4d19002"),
                    Name = "Employee",
                    NormalizedName = "EMPLOYEE",
                    Description = "Standard application user"
                });
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("UserRoles");
            entity.HasKey(membership => new { membership.UserId, membership.RoleId });
            entity.Property(membership => membership.AssignedAtUtc).IsRequired();
            entity.HasOne(membership => membership.User)
                .WithMany(user => user.UserRoles)
                .HasForeignKey(membership => membership.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(membership => membership.Role)
                .WithMany(role => role.UserRoles)
                .HasForeignKey(membership => membership.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(membership => membership.AssignedByUser)
                .WithMany()
                .HasForeignKey(membership => membership.AssignedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<RecoveryCode>(entity =>
        {
            entity.ToTable("RecoveryCodes");
            entity.HasKey(code => code.Id);
            entity.Property(code => code.CodeDigest).IsRequired().HasMaxLength(64);
            entity.Property(code => code.IssuedAtUtc).IsRequired();
            entity.HasIndex(code => code.UserId)
                .IsUnique()
                .HasFilter("\"UsedAtUtc\" IS NULL AND \"RevokedAtUtc\" IS NULL");
            entity.HasOne(code => code.User)
                .WithMany(user => user.RecoveryCodes)
                .HasForeignKey(code => code.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SecurityEvent>(entity =>
        {
            entity.ToTable("SecurityEvents");
            entity.HasKey(entry => entry.Id);
            entity.Property(entry => entry.EventType).IsRequired().HasMaxLength(100);
            entity.Property(entry => entry.Outcome).IsRequired().HasMaxLength(30);
            entity.Property(entry => entry.CreatedAtUtc).IsRequired();
            entity.Property(entry => entry.Details).HasMaxLength(1000);
            entity.HasIndex(entry => entry.CreatedAtUtc);
            entity.HasOne(entry => entry.User)
                .WithMany()
                .HasForeignKey(entry => entry.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AppMetadata>(entity =>
        {
            entity.ToTable("AppMetadata");
            entity.HasKey(metadata => metadata.Key);
            entity.Property(metadata => metadata.Key).HasMaxLength(100);
            entity.Property(metadata => metadata.Value).IsRequired().HasMaxLength(500);
        });
    }
}
