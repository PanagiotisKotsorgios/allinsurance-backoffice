using AllInsurance.Application.Abstractions;
using AllInsurance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using AllInsurance.Application.Authorization;

namespace AllInsurance.Infrastructure.Persistence;

public sealed class AllInsuranceDbContext(DbContextOptions<AllInsuranceDbContext> options,
    ISessionService? session = null)
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
    public DbSet<CustomerActivity> CustomerActivities => Set<CustomerActivity>();
    public DbSet<BusinessRecord> BusinessRecords => Set<BusinessRecord>();
    public DbSet<BusinessRecordActivity> BusinessRecordActivities => Set<BusinessRecordActivity>();
    public DbSet<BusinessRecordAttachment> BusinessRecordAttachments => Set<BusinessRecordAttachment>();
    public DbSet<PartnerPayout> PartnerPayouts => Set<PartnerPayout>();
    public DbSet<PartnerActivity> PartnerActivities => Set<PartnerActivity>();
    public DbSet<CommissionRule> CommissionRules => Set<CommissionRule>();
    public DbSet<OfficeOperation> OfficeOperations => Set<OfficeOperation>();
    public DbSet<FilterOption> FilterOptions => Set<FilterOption>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        RecordChanges();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void RecordChanges()
    {
        var actor = session?.Current;
        if (actor is null) return;
        ChangeTracker.DetectChanges();
        foreach (var entry in ChangeTracker.Entries().ToArray())
        {
            if (entry.Entity is SecurityEvent or User or UserRole or RecoveryCode ||
                entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;
            var operation = entry.State switch
            {
                EntityState.Added => "CREATE",
                EntityState.Modified => "UPDATE",
                _ => "DELETE"
            };
            var changed = entry.State == EntityState.Modified
                ? string.Join(",", entry.Properties.Where(property => property.IsModified)
                    .Select(property => property.Metadata.Name)) : string.Empty;
            if (entry.State == EntityState.Modified && changed.Length == 0) continue;
            var key = string.Join(",", entry.Properties.Where(property => property.Metadata.IsPrimaryKey())
                .Select(property => property.CurrentValue?.ToString() ?? string.Empty));
            var details = $"entity_id={key}; fields={changed}";
            SecurityEvents.Add(new SecurityEvent
            {
                Id = Guid.NewGuid(), UserId = actor.UserId,
                EventType = $"{operation}:{entry.Metadata.ClrType.Name}", Outcome = "SUCCESS",
                CreatedAtUtc = DateTime.UtcNow,
                Details = details[..Math.Min(details.Length, 1000)]
            });
        }
    }

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
            entity.Property(customer => customer.ThirdMobile).HasMaxLength(40);
            entity.Property(customer => customer.InsuranceRegistry).HasMaxLength(80);
            entity.Property(customer => customer.LegalForm).HasMaxLength(100);
            entity.Property(customer => customer.BusinessActivityCode).HasMaxLength(80);
            entity.Property(customer => customer.Employer).HasMaxLength(160);
            entity.Property(customer => customer.Source).HasMaxLength(160);
            entity.Property(customer => customer.ReferredByPartnerName).HasMaxLength(160);
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
            entity.Property(transaction => transaction.PaymentMethod).IsRequired().HasMaxLength(80)
                .HasDefaultValue("Μετρητά");
            entity.Property(transaction => transaction.Currency).IsRequired().HasMaxLength(3)
                .HasDefaultValue("EUR");
            entity.Property(transaction => transaction.Reference).HasMaxLength(100);
            entity.Property(transaction => transaction.Description).HasMaxLength(500);
            entity.HasOne(transaction => transaction.BusinessRecord).WithMany()
                .HasForeignKey(transaction => transaction.BusinessRecordId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(transaction => new { transaction.CustomerId, transaction.OccurredAtUtc });
            entity.HasOne(transaction => transaction.Customer).WithMany(customer => customer.Transactions)
                .HasForeignKey(transaction => transaction.CustomerId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CustomerActivity>(entity =>
        {
            entity.ToTable("CustomerActivities");
            entity.HasKey(activity => activity.Id);
            entity.Property(activity => activity.Action).IsRequired().HasMaxLength(80);
            entity.Property(activity => activity.Description).IsRequired().HasMaxLength(4000);
            entity.Property(activity => activity.ActorName).IsRequired().HasMaxLength(200);
            entity.HasIndex(activity => new { activity.CustomerId, activity.OccurredAtUtc });
            entity.HasOne(activity => activity.Customer).WithMany(customer => customer.Activities)
                .HasForeignKey(activity => activity.CustomerId).OnDelete(DeleteBehavior.Cascade);
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
            entity.Property(record => record.DocumentType).HasMaxLength(40);
            entity.Property(record => record.DetailsJson).HasMaxLength(100000);
            entity.HasIndex(record => new { record.SectionKey, record.Title });
            entity.HasIndex(record => record.CustomerId);
            entity.HasOne(record => record.Customer).WithMany(customer => customer.BusinessRecords)
                .HasForeignKey(record => record.CustomerId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PartnerPayout>(entity =>
        {
            entity.ToTable("PartnerPayouts");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.Property(item => item.Currency).IsRequired().HasMaxLength(3);
            entity.Property(item => item.PaymentMethod).IsRequired().HasMaxLength(80);
            entity.Property(item => item.Kind).IsRequired().HasMaxLength(12).HasDefaultValue("payment");
            entity.Property(item => item.Reference).HasMaxLength(100);
            entity.Property(item => item.Description).HasMaxLength(500);
            entity.HasIndex(item => new { item.PartnerId, item.OccurredAtUtc });
            entity.HasOne(item => item.Partner).WithMany()
                .HasForeignKey(item => item.PartnerId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PartnerActivity>(entity =>
        {
            entity.ToTable("PartnerActivities");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Action).IsRequired().HasMaxLength(80);
            entity.Property(item => item.Description).IsRequired().HasMaxLength(4000);
            entity.Property(item => item.ActorName).IsRequired().HasMaxLength(200);
            entity.HasIndex(item => new { item.PartnerId, item.OccurredAtUtc });
            entity.HasOne(item => item.Partner).WithMany()
                .HasForeignKey(item => item.PartnerId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BusinessRecordActivity>(entity =>
        {
            entity.ToTable("BusinessRecordActivities");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Action).IsRequired().HasMaxLength(80);
            entity.Property(item => item.Description).IsRequired().HasMaxLength(4000);
            entity.Property(item => item.ActorName).IsRequired().HasMaxLength(200);
            entity.HasIndex(item => new { item.RecordId, item.OccurredAtUtc });
            entity.HasOne(item => item.Record).WithMany()
                .HasForeignKey(item => item.RecordId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BusinessRecordAttachment>(entity =>
        {
            entity.ToTable("BusinessRecordAttachments");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.FileName).IsRequired().HasMaxLength(260);
            entity.Property(item => item.RelativePath).IsRequired().HasMaxLength(320);
            entity.Property(item => item.UploadedBy).IsRequired().HasMaxLength(200);
            entity.HasIndex(item => new { item.RecordId, item.UploadedAtUtc });
            entity.HasOne(item => item.Record).WithMany()
                .HasForeignKey(item => item.RecordId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CommissionRule>(entity =>
        {
            entity.ToTable("CommissionRules");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Insurer).IsRequired().HasMaxLength(200);
            entity.Property(item => item.Branch).HasMaxLength(120);
            entity.Property(item => item.VehicleUse).HasMaxLength(120);
            entity.Property(item => item.Coverage).HasMaxLength(4000);
            entity.Property(item => item.Package).HasMaxLength(200);
            entity.Property(item => item.OfficePercent).HasPrecision(5, 2);
            entity.Property(item => item.PartnerPercent).HasPrecision(5, 2);
            entity.HasIndex(item => new { item.Insurer, item.IsActive });
        });

        modelBuilder.Entity<OfficeOperation>(entity =>
        {
            entity.ToTable("OfficeOperations");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Kind).IsRequired().HasMaxLength(24);
            entity.Property(item => item.Description).IsRequired().HasMaxLength(300);
            entity.Property(item => item.Category).HasMaxLength(100);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.Property(item => item.PaymentMethod).HasMaxLength(80);
            entity.Property(item => item.Reference).HasMaxLength(100);
            entity.Property(item => item.Notes).HasMaxLength(2000);
            entity.Property(item => item.GrossPremium).HasPrecision(18, 2);
            entity.Property(item => item.NetPremium).HasPrecision(18, 2);
            entity.Property(item => item.OfficeCommission).HasPrecision(18, 2);
            entity.Property(item => item.OverCommission).HasPrecision(18, 2);
            entity.HasIndex(item => new { item.Kind, item.OccurredOn });
            entity.HasOne<BusinessRecord>().WithMany().HasForeignKey(item => item.PartnerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<BusinessRecord>().WithMany().HasForeignKey(item => item.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
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
