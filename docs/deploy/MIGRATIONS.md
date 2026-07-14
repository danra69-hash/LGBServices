# Database migrations (Wave 4)

Production (Railway Docker) uses **SQLite** at `/data/lgbapp.db`.

## Boot path

1. `DatabaseBootstrap.ApplyMigrations`
2. If the DB already has tables but **no** `__EFMigrationsHistory` (legacy `EnsureCreated` DBs), stamp `20260714084624_Baseline_FullSchema` as applied
3. `Database.Migrate()`
4. **Coexistence:** `SqliteSchemaMigrator.Apply` still runs once so older volumes stay patched

## After this release is stable

1. Confirm Railway `/data/lgbapp.db` has `__EFMigrationsHistory` with the baseline id
2. Set `runSqliteHandMigrator: false` in `Program.cs` (or remove the call)
3. Delete `Data/SqliteSchemaMigrator.cs` when no longer needed
4. Add new schema only via `dotnet ef migrations add …`

## Local commands

```bash
export DOTNET_ROOT="$HOME/.local/dotnet"   # if needed
export PATH="$DOTNET_ROOT:$HOME/.dotnet/tools:$PATH"

dotnet ef migrations add <Name> \
  --project LGBApp.Backend \
  --startup-project LGBApp.Backend
```

Design-time always uses SQLite (`DesignTimeDbContextFactory`).

## SQL Server

`Database:Provider=SqlServer` also calls `Migrate()`. Schema was generated against SQLite type mappings; prefer SQLite in production. If you need first-class SQL Server, regenerate a provider-specific migration set.

## Backup

Before deploying this change to Railway, snapshot the volume (`/data/lgbapp.db`). Existing data is preserved via baseline stamping — do **not** delete the volume.
