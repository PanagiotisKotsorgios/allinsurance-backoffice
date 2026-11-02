using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AllInsurance.Infrastructure.Persistence;

public sealed class AllInsuranceDesignTimeDbContextFactory : IDesignTimeDbContextFactory<AllInsuranceDbContext>
{
    public AllInsuranceDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AllInsuranceDbContext>()
            .UseSqlite("Data Source=allinsurance-design-time.db")
            .Options;
        return new AllInsuranceDbContext(options);
    }
}
