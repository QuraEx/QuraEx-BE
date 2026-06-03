using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace QuraEx.Workspace.Infrastructure;

// EF design-time factory — used by `dotnet ef migrations` and `has-pending-model-changes`
// when no live host is available. Dummy connection string is fine (no DB needed for snapshots).
internal sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // S2068 suppressed: dummy localhost-only connection string for EF design-time tools,
        // never used at runtime; not a real credential.
#pragma warning disable S2068
        const string designTimeConnStr =
            "Host=localhost;Database=workspace_design;Username=postgres;Password=postgres";
#pragma warning restore S2068

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(designTimeConnStr, npgsql => npgsql.EnableRetryOnFailure(0))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(options);
    }
}
