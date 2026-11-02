using AllInsurance.Application.Abstractions;
using AllInsurance.Infrastructure.Persistence;
using AllInsurance.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AllInsurance.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAllInsuranceInfrastructure(
        this IServiceCollection services,
        string databasePath)
    {
        var connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            ForeignKeys = true,
            DefaultTimeout = 5
        }.ToString();

        services.AddDbContextFactory<AllInsuranceDbContext>(options => options.UseSqlite(connectionString));
        services.AddSingleton<IApplicationDbContextFactory, ApplicationDbContextFactory>();
        services.AddSingleton<IStartupInitializer>(provider =>
            new DatabaseInitializer(
                provider.GetRequiredService<IDbContextFactory<AllInsuranceDbContext>>(),
                databasePath));
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
        services.AddSingleton<IRecoveryCodeGenerator, CryptographicRecoveryCodeGenerator>();
        return services;
    }
}
