using AllInsurance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace AllInsurance.Application.Abstractions;

public interface IApplicationDbContext : IAsyncDisposable
{
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<RecoveryCode> RecoveryCodes { get; }
    DbSet<SecurityEvent> SecurityEvents { get; }
    DbSet<AppMetadata> AppMetadata { get; }
    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IApplicationDbContextFactory
{
    ValueTask<IApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default);
}
