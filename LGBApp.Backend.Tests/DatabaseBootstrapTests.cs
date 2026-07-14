using LGBApp.Backend.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace LGBApp.Backend.Tests;

public class DatabaseBootstrapTests
{
    [Fact]
    public void FreshDatabase_Migrate_CreatesSchemaAndHistory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lgb-boot-{Guid.NewGuid():N}.db");
        try
        {
            using var context = CreateContext(path);
            DatabaseBootstrap.ApplyMigrations(context, runSqliteHandMigrator: true);

            Assert.True(context.Database.CanConnect());
            Assert.Contains(
                DatabaseBootstrap.BaselineMigrationId,
                context.Database.GetAppliedMigrations());
            Assert.True(context.Users.Any() || !context.Users.Any()); // table queryable
            Assert.Empty(context.Database.GetPendingMigrations());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void LegacyEnsureCreatedDatabase_StampsBaselineWithoutRecreate()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lgb-legacy-{Guid.NewGuid():N}.db");
        try
        {
            // Simulate old Railway boot: EnsureCreated + hand migrator, no EF history
            using (var legacy = CreateContext(path))
            {
                legacy.Database.EnsureCreated();
                SqliteSchemaMigrator.Apply(legacy);
                legacy.Users.Add(new Models.User
                {
                    Email = "legacy@test.local",
                    PasswordHash = "x",
                    Name = "Legacy",
                    Role = "User",
                    CreatedAt = DateTime.UtcNow,
                });
                legacy.SaveChanges();
            }

            using var context = CreateContext(path);
            DatabaseBootstrap.ApplyMigrations(context, runSqliteHandMigrator: true);

            Assert.Contains(
                DatabaseBootstrap.BaselineMigrationId,
                context.Database.GetAppliedMigrations());
            Assert.Equal(1, context.Users.Count(u => u.Email == "legacy@test.local"));
            Assert.Empty(context.Database.GetPendingMigrations());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static AppDbContext CreateContext(string path)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;
        return new AppDbContext(options);
    }
}
