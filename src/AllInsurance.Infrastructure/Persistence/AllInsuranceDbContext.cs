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
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerDetail> CustomerDetails => Set<CustomerDetail>();
    public DbSet<CustomerTransaction> CustomerTransactions => Set<CustomerTransaction>();
    public DbSet<BusinessRecord> BusinessRecords => Set<BusinessRecord>();
    public DbSet<FilterOption> FilterOptions => Set<FilterOption>();

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

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customers");
            entity.HasKey(customer => customer.Id);
            entity.Property(customer => customer.LastName).IsRequired().HasMaxLength(150);
            entity.Property(customer => customer.FirstName).IsRequired().HasMaxLength(150);
            entity.Property(customer => customer.CompanyName).HasMaxLength(200);
            entity.Property(customer => customer.CustomerKind).IsRequired().HasMaxLength(40);
            entity.Property(customer => customer.PrimaryAddress).HasMaxLength(300);
            entity.Property(customer => customer.City).HasMaxLength(120);
            entity.Property(customer => customer.Region).HasMaxLength(120);
            entity.Property(customer => customer.PostalCode).HasMaxLength(20);
            entity.Property(customer => customer.Gender).HasMaxLength(40);
            entity.Property(customer => customer.SocialSecurityNumber).HasMaxLength(20);
            entity.Property(customer => customer.MaritalStatus).HasMaxLength(80);
            entity.Property(customer => customer.SpouseName).HasMaxLength(160);
            entity.Property(customer => customer.HomePhone).HasMaxLength(40);
            entity.Property(customer => customer.SecondMobile).HasMaxLength(40);
            entity.Property(customer => customer.InsuranceRegistry).HasMaxLength(80);
            entity.Property(customer => customer.LegalForm).HasMaxLength(100);
            entity.Property(customer => customer.BusinessActivityCode).HasMaxLength(80);
            entity.Property(customer => customer.Employer).HasMaxLength(160);
            entity.Property(customer => customer.Source).HasMaxLength(160);
            entity.Property(customer => customer.Tags).HasMaxLength(500);
            entity.Property(customer => customer.PhotoUrl).HasMaxLength(500);
            entity.Property(customer => customer.PassportNumber).HasMaxLength(50);
            entity.Property(customer => customer.Zone).HasMaxLength(120);
            entity.Property(customer => customer.PrimaryMobile).HasMaxLength(40);
            entity.Property(customer => customer.PrimaryEmail).HasMaxLength(254);
            entity.Property(customer => customer.TaxNumber).HasMaxLength(9);
            entity.HasIndex(customer => customer.TaxNumber).IsUnique().HasFilter("\"TaxNumber\" IS NOT NULL");
            entity.Property(customer => customer.OpeningBalance).HasPrecision(18, 2);
            entity.HasIndex(customer => new { customer.LastName, customer.FirstName });
            entity.HasOne(customer => customer.ReferredByCustomer)
                .WithMany().HasForeignKey(customer => customer.ReferredByCustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CustomerDetail>(entity =>
        {
            entity.ToTable("CustomerDetails");
            entity.HasKey(detail => detail.Id);
            entity.Property(detail => detail.Category).IsRequired().HasMaxLength(40);
            entity.Property(detail => detail.Title).IsRequired().HasMaxLength(200);
            entity.Property(detail => detail.Value).HasMaxLength(1000);
            entity.Property(detail => detail.Notes).HasMaxLength(2000);
            entity.HasIndex(detail => new { detail.CustomerId, detail.Category });
            entity.HasOne(detail => detail.Customer).WithMany(customer => customer.Details)
                .HasForeignKey(detail => detail.CustomerId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CustomerTransaction>(entity =>
        {
            entity.ToTable("CustomerTransactions");
            entity.HasKey(transaction => transaction.Id);
            entity.Property(transaction => transaction.Type).IsRequired().HasMaxLength(20);
            entity.Property(transaction => transaction.Amount).HasPrecision(18, 2);
            entity.Property(transaction => transaction.Description).HasMaxLength(500);
            entity.HasIndex(transaction => new { transaction.CustomerId, transaction.OccurredAtUtc });
            entity.HasOne(transaction => transaction.Customer).WithMany(customer => customer.Transactions)
                .HasForeignKey(transaction => transaction.CustomerId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BusinessRecord>(entity =>
        {
            entity.ToTable("BusinessRecords");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.SectionKey).IsRequired().HasMaxLength(40);
            entity.Property(record => record.Title).IsRequired().HasMaxLength(200);
            entity.Property(record => record.Reference).HasMaxLength(100);
            entity.Property(record => record.RelatedParty).HasMaxLength(200);
            entity.Property(record => record.Status).HasMaxLength(100);
            entity.Property(record => record.Notes).HasMaxLength(2000);
            entity.HasIndex(record => new { record.SectionKey, record.Title });
            entity.HasIndex(record => record.CustomerId);
            entity.HasOne(record => record.Customer).WithMany(customer => customer.BusinessRecords)
                .HasForeignKey(record => record.CustomerId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<FilterOption>(entity =>
        {
            entity.ToTable("FilterOptions");
            entity.HasKey(option => option.Id);
            entity.Property(option => option.Category).IsRequired().HasMaxLength(80);
            entity.Property(option => option.Value).IsRequired().HasMaxLength(160);
            entity.HasIndex(option => new { option.Category, option.Value }).IsUnique();
        });
    }
}
