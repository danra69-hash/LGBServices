using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LGBApp.Backend.Data;

/// <summary>Design-time factory so <c>dotnet ef migrations</c> targets SQLite (production provider).</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=ef-design-time.db")
            .Options;
        return new AppDbContext(options);
    }
}
