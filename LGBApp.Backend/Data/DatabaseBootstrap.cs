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

        // Coexistence: SqliteSchemaMigrator (or a partially-applied EF migration) may have
        // already created objects that the next EF migration tries to recreate. Stamp only
        // the failing head migration, then continue so later migrations still apply.
        for (var attempt = 0; attempt < 8; attempt++)
        {
            try
            {
                context.Database.Migrate();
                break;
            }
            catch (Exception ex) when (context.Database.IsSqlite() && IsAlreadyExistsError(ex))
            {
                var head = context.Database.GetPendingMigrations().FirstOrDefault();
                if (head == null)
                    throw;

                Console.WriteLine(
                    $"[Startup] EF Migrate hit already-applied SQLite object while applying '{head}' — stamping it and retrying. "
                    + ex.Message);
                StampMigration(context, head);
            }
        }

        // Coexistence: keep hand migrator until the next release after EF history is live.
        if (runSqliteHandMigrator && context.Database.IsSqlite())
            SqliteSchemaMigrator.Apply(context);
    }

    private static bool IsAlreadyExistsError(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException!)
        {
            var msg = e.Message ?? string.Empty;
            if (msg.Contains("already exists", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("duplicate column", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static void StampMigration(AppDbContext context, string migrationId)
    {
        var history = context.GetService<IHistoryRepository>();
        if (!history.Exists())
            context.Database.ExecuteSqlRaw(history.GetCreateScript());

        if (history.GetAppliedMigrations().Any(h => h.MigrationId == migrationId))
            return;

        context.Database.ExecuteSqlRaw(
            history.GetInsertScript(new HistoryRow(migrationId, EfProductVersion)));
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

        // Stamp every pending migration. Legacy EnsureCreated DBs already match the live
        // model (plus SqliteSchemaMigrator). Environments that already have history apply
        // new migrations normally via Migrate() on the next lines.
        var pending = context.Database.GetPendingMigrations().ToList();
        if (pending.Count == 0)
            return;

        Console.WriteLine(
            "[Startup] Existing database has no EF migration history — stamping "
            + string.Join(", ", pending)
            + " (legacy EnsureCreated / SqliteSchemaMigrator).");

        if (!history.Exists())
            context.Database.ExecuteSqlRaw(history.GetCreateScript());

        foreach (var migrationId in pending)
        {
            context.Database.ExecuteSqlRaw(
                history.GetInsertScript(new HistoryRow(migrationId, EfProductVersion)));
        }
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
            else if (context.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
            {
                // EF quotes PascalCase table names on Postgres ("Users").
                command.CommandText =
                    """SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = @name LIMIT 1;""";
                var p = command.CreateParameter();
                p.ParameterName = "@name";
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
