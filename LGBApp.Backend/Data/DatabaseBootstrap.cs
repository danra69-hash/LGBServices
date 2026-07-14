using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;

namespace LGBApp.Backend.Data;

/// <summary>
/// Wave 4: EF <c>Migrate()</c> for Sqlite + SqlServer, with a one-release coexistence
/// path that still runs <see cref="SqliteSchemaMigrator"/> for legacy Railway volumes.
/// </summary>
public static class DatabaseBootstrap
{
    /// <summary>Must match the generated migration id in <c>Migrations/</c>.</summary>
    public const string BaselineMigrationId = "20260714084624_Baseline_FullSchema";

    public const string EfProductVersion = "8.0.11";

    /// <summary>
    /// Apply pending EF migrations. If the DB was created with <c>EnsureCreated</c>
    /// (no <c>__EFMigrationsHistory</c>), stamp the baseline so Migrate() does not
    /// try to recreate existing tables.
    /// </summary>
    public static void ApplyMigrations(AppDbContext context, bool runSqliteHandMigrator)
    {
        if (!context.Database.IsRelational())
            return;

        StampBaselineIfLegacyDatabase(context);
        context.Database.Migrate();

        // Coexistence: keep hand migrator until the next release after EF history is live.
        if (runSqliteHandMigrator && context.Database.IsSqlite())
            SqliteSchemaMigrator.Apply(context);
    }

    private static void StampBaselineIfLegacyDatabase(AppDbContext context)
    {
        if (!context.Database.CanConnect())
            return;

        var history = context.GetService<IHistoryRepository>();
        var applied = history.Exists()
            ? history.GetAppliedMigrations()
            : Array.Empty<HistoryRow>();

        if (applied.Count > 0)
            return;

        if (!TableExists(context, "Users"))
            return;

        Console.WriteLine(
            "[Startup] Existing database has no EF migration history — stamping "
            + BaselineMigrationId + " (legacy EnsureCreated / SqliteSchemaMigrator).");

        if (!history.Exists())
            context.Database.ExecuteSqlRaw(history.GetCreateScript());

        context.Database.ExecuteSqlRaw(
            history.GetInsertScript(new HistoryRow(BaselineMigrationId, EfProductVersion)));
    }

    private static bool TableExists(AppDbContext context, string table)
    {
        var connection = context.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;
        if (!wasOpen)
            connection.Open();

        try
        {
            using var command = connection.CreateCommand();
            if (context.Database.IsSqlite())
            {
                command.CommandText =
                    """SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name LIMIT 1;""";
                var p = command.CreateParameter();
                p.ParameterName = "$name";
                p.Value = table;
                command.Parameters.Add(p);
            }
            else
            {
                command.CommandText =
                    """SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @name;""";
                var p = command.CreateParameter();
                p.ParameterName = "@name";
                p.Value = table;
                command.Parameters.Add(p);
            }

            return command.ExecuteScalar() != null;
        }
        finally
        {
            if (!wasOpen)
                connection.Close();
        }
    }
}
