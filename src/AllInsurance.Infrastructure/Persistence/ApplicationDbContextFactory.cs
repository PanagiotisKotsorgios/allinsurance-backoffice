using AllInsurance.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace AllInsurance.Infrastructure.Persistence;

public sealed class ApplicationDbContextFactory(IDbContextFactory<AllInsuranceDbContext> factory)
    : IApplicationDbContextFactory
{
    public async ValueTask<IApplicationDbContext> CreateDbContextAsync(
        CancellationToken cancellationToken = default) =>
        await factory.CreateDbContextAsync(cancellationToken);
}
