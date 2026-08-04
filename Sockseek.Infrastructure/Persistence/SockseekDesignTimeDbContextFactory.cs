using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sockseek.Infrastructure.Persistence;

public sealed class SockseekDesignTimeDbContextFactory : IDesignTimeDbContextFactory<SockseekDbContext>
{
    public SockseekDbContext CreateDbContext(string[] args)
    {
        string connectionString = args.FirstOrDefault(arg => arg.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
            ?? "Data Source=sockseek-dev.db";

        var optionsBuilder = new DbContextOptionsBuilder<SockseekDbContext>();
        optionsBuilder.UseSqlite(connectionString);
        return new SockseekDbContext(optionsBuilder.Options);
    }
}
