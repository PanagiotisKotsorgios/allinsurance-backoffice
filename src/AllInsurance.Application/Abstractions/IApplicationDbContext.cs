using AllInsurance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AllInsurance.Application.Abstractions;

public interface IApplicationDbContext : IAsyncDisposable
{
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<RecoveryCode> RecoveryCodes { get; }
    DbSet<SecurityEvent> SecurityEvents { get; }
    DbSet<AppMetadata> AppMetadata { get; }
    DbSet<Customer> Customers { get; }
    DbSet<CustomerDetail> CustomerDetails { get; }
    DbSet<CustomerTransaction> CustomerTransactions { get; }
    DbSet<CustomerActivity> CustomerActivities { get; }
    DbSet<BusinessRecord> BusinessRecords { get; }
    DbSet<FilterOption> FilterOptions { get; }
    DatabaseFacade Database { get; }
    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IApplicationDbContextFactory
{
    ValueTask<IApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default);
}
