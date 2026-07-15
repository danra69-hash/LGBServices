# LGBServices — SQLite → PostgreSQL Migration Guide

Audience: an implementing model executing this step by step. Goal: move the backend from SQLite to PostgreSQL **without losing any data, any relationship (foreign key), or any startup initializer/seeder**, and keep SQL Server and SQLite still working as options. Follow the steps in order. Each step has an **acceptance check** — do not proceed until it passes.

> ⚠️ **Read this first — the three things being migrated are separate.**
> 1. **Schema** (tables, columns, foreign keys, indexes) — defined in `Data/AppDbContext.cs` `OnModelCreating` + EF migrations.
> 2. **Data** (rows: users, customers, jobs, forms) — lives in the SQLite file at the `ConnectionStrings:DefaultConnection` path (production: `/data/lgbapp.db`).
> 3. **Uploaded files** (MOI/MOA/supporting documents) — **NOT in the database.** Only their metadata rows are. The bytes live on disk under `LGB_UPLOAD_ROOT` (production: `/data/uploads`). Migrating the DB does **not** move these files. See §9.
>
> The user has "a lot of files and people" — so §8 (data copy) and §9 (files) are as important as the schema. Do not skip them.

---

## 0. What must NOT be deleted or regenerated from scratch

These are the "relationships and initialisations" the request calls out. Preserve them exactly:

- **All relationship config in `Data/AppDbContext.cs` `OnModelCreating`** — every `HasOne/WithMany/HasForeignKey/OnDelete`. These are **provider-agnostic**; they work identically on Postgres. Do not remove or rewrite them. (Cascade/SetNull behaviors are part of the data contract.)
- **The `RotateFormConcurrencyStamps()` override and `IsConcurrencyToken()` config** — the MOI/MOA optimistic-concurrency guard. Keep it; §6 covers the Guid type.
- **All startup seeders/initializers in `Program.cs`**: `WorkflowConfigSeeder.Seed` / `SeedReferenceData`, `InternalStaffSeeder.Seed`, `FigmaProductCatalog.SyncCatalog`, `FormCustomerIdBackfill.Apply`, and (under `SEED_FULL`) `CubeVCustomerSeeder`, `BillingPartyService`, `CustomerClientAdminProvisioner`, `CustomerSignatoryProvisioner`, `SignatoryAccessService.BackfillFromHolders`, `JobRequestSyncService`, `JobWorkflowIntegrityService`, `JobFormProvisioner`. These are EF operations and are provider-agnostic — they must still run on Postgres. Do not delete any.
- **The existing SQLite and SqlServer providers.** This migration **adds** Postgres; it does not remove the others.
- **`Data/SqliteSchemaMigrator.cs`** — keep the file, but it must **never run against Postgres** (it is raw SQLite DDL). §7 gates it.

**Do not** hand-edit any file under `Migrations/`, `obj/`, `bin/`, or `out/`.

---

## 1. Take a backup (non-negotiable)

```bash
# From repo root. Copy the live SQLite DB and the uploads dir before touching anything.
cp LGBApp.Backend/lgbapp.db  ~/lgb-backup-$(date +%Y%m%d).db          # or the prod /data/lgbapp.db
cp -r LGBApp.Backend/uploads ~/lgb-uploads-backup-$(date +%Y%m%d)      # or /data/uploads in prod
git switch -c postgres-migration                                       # work on a branch
```
**Acceptance**: both backup copies exist and are non-empty. You are on a new branch.

---

## 2. Add the Npgsql provider package

Edit `LGBApp.Backend/LGBApp.Backend.csproj` and add, next to the existing EF providers:
```xml
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.11" />
```
(Match the EF Core major version already in use — **8.0.x**. Do not upgrade EF Core itself in this migration.)

```bash
/Users/ryannnism/.local/dotnet/dotnet restore LGBApp.Backend/LGBApp.Backend.csproj
```
**Acceptance**: `dotnet build LGBApp.Backend/LGBApp.Backend.csproj` succeeds with 0 errors.

---

## 3. Wire the provider switch (add "Postgres" everywhere "Sqlite" is chosen)

There are **four** switch points. Update all four. The config key is `Database:Provider` (env `Database__Provider`).

1. **`Program.cs` (~line 53)** — DbContext registration:
```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (string.Equals(dbProvider, "Sqlite", StringComparison.OrdinalIgnoreCase))
        options.UseSqlite(connectionString);
    else if (string.Equals(dbProvider, "Postgres", StringComparison.OrdinalIgnoreCase)
             || string.Equals(dbProvider, "Postgresql", StringComparison.OrdinalIgnoreCase))
        options.UseNpgsql(connectionString);
    else
        options.UseSqlServer(connectionString);
});
```

2. **`Program.cs` (~line 210)** — the Sqlite-specific startup branch. This branch currently runs `DatabaseBootstrap.ApplyMigrations(..., runSqliteHandMigrator: true)` and light/full seed. Restructure so Postgres uses `Migrate()` + the **same seeders** but **never** the SQLite hand migrator:
```csharp
var isSqlite  = string.Equals(dbProvider, "Sqlite",  StringComparison.OrdinalIgnoreCase);
var isPostgres= string.Equals(dbProvider, "Postgres",StringComparison.OrdinalIgnoreCase)
             || string.Equals(dbProvider, "Postgresql",StringComparison.OrdinalIgnoreCase);

DatabaseBootstrap.ApplyMigrations(context, runSqliteHandMigrator: isSqlite);
// then run the SAME seeders that the Sqlite branch runs today (WorkflowConfigSeeder, InternalStaffSeeder,
// FigmaProductCatalog.SyncCatalog, FormCustomerIdBackfill, and the SEED_FULL block). Do NOT drop any.
```
Keep the existing seeder calls verbatim — just make them run for Postgres too. The seeders are idempotent (they check-then-insert), so they are safe.

3. **`Data/DevDatabaseReset.cs` (~line 16)** — provider read; add Postgres handling analogous to Sqlite (used by the `reset-dev-db` CLI command). If it has SQLite-specific file-delete logic, guard it so Postgres uses `EnsureDeleted()/Migrate()` instead of deleting a file.

4. **`Data/DesignTimeDbContextFactory.cs` (~line 12)** — used by `dotnet ef` at design time. Make it honor an env var so you can generate Postgres migrations:
```csharp
var provider = Environment.GetEnvironmentVariable("Database__Provider") ?? "Sqlite";
var conn = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
           ?? "Data Source=ef-design-time.db";
if (provider.StartsWith("Postgres", StringComparison.OrdinalIgnoreCase))
    builder.UseNpgsql(conn);
else if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
    builder.UseSqlServer(conn);
else
    builder.UseSqlite(conn);
```

**Acceptance**: build succeeds; `grep -rn "UseNpgsql" LGBApp.Backend --include=*.cs` shows it in `Program.cs` and `DesignTimeDbContextFactory.cs`.

---

## 4. `DatabaseBootstrap` must not run SQLite DDL on Postgres

Open `Data/DatabaseBootstrap.cs`. Confirm the hand migrator only runs when `context.Database.IsSqlite()` — it already does (`runSqliteHandMigrator && context.Database.IsSqlite()`). Do not change that guard. `context.Database.Migrate()` is provider-agnostic and will apply the Postgres migrations you generate in §5.

`StampBaselineIfLegacyDatabase` stamps a baseline when it finds pre-existing tables with no `__EFMigrationsHistory`. On a **brand-new empty Postgres database** there are no tables, so it will correctly skip stamping and `Migrate()` will build everything. Leave it as-is.

**Acceptance**: reading the file, the only `SqliteSchemaMigrator.Apply` call is behind `IsSqlite()`.

---

## 5. Generate a **Postgres-specific** migration set (you cannot reuse the SQLite ones)

The migrations in `Migrations/` were generated for SQLite: they carry `.Annotation("Sqlite:Autoincrement", true)` and `type: "TEXT"/"INTEGER"` columns. **These will not produce a correct Postgres schema.** EF stores migrations per-DbContext, not per-provider, so the clean approach is a **separate migrations output folder for Postgres** selected at design time. Simplest reliable path:

1. Spin up a local Postgres (Docker):
```bash
docker run --name lgb-pg -e POSTGRES_PASSWORD=lgb -e POSTGRES_DB=lgbapp -p 5432:5432 -d postgres:16
```
2. Generate a Postgres baseline migration into a dedicated folder:
```bash
export Database__Provider=Postgres
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=lgbapp;Username=postgres;Password=lgb"
/Users/ryannnism/.local/dotnet/dotnet ef migrations add Pg_Baseline \
  --project LGBApp.Backend/LGBApp.Backend.csproj \
  --output-dir Migrations/Postgres
```
3. Apply it:
```bash
/Users/ryannnism/.local/dotnet/dotnet ef database update \
  --project LGBApp.Backend/LGBApp.Backend.csproj
```

> If `dotnet ef` is not installed: `dotnet tool install --global dotnet-ef --version 8.0.11`.

> **Why a separate folder, and the caveat**: EF applies *all* migrations found for the context. If you keep the SQLite migrations and the Postgres migration in the same assembly, `Migrate()` will try to run both and fail. Two clean options — pick ONE and tell the reviewer which:
> - **(A) Recommended for a single-DB deployment**: since production is moving to Postgres, generate the Postgres baseline as the *only* migration the app ships with, and keep the SQLite ones only for local dev via a provider-conditional migrations assembly. If that is too advanced, use option B.
> - **(B) Simplest**: create a **new git branch/build that targets Postgres only** — delete the SQLite-specific migrations from `Migrations/` on that branch (the SQLite dev path uses `EnsureCreated`/hand migrator anyway via `DatabaseBootstrap`), and keep only `Migrations/Postgres/*`. Do **not** delete `SqliteSchemaMigrator.cs`. Confirm `dotnet test` still passes (tests use an in-memory/SQLite test DB via `TestDbFactory` and do not depend on the Postgres migrations).
>
> **Implemented: (A)** — `ProviderFilteredMigrationsAssembly` filters by namespace (`Migrations.Postgres` vs the rest). SQLite migrations remain under `Migrations/`; Postgres baseline is `Migrations/Postgres/Pg_Baseline` (+ its own `AppDbContextModelSnapshot`). Both providers keep working until cutover.

**Acceptance**: `psql` (or `\dt`) against the new Postgres DB shows **every** table that exists in the SQLite DB — cross-check with the list below. Foreign keys exist (see §10 check).

Expected tables (from `AppDbContext` DbSets): Users, Customers, CustomerPackages, AccountHolders, Products, JobRequests, JobRequestUnits, JobRequestUnitAssignees, CompletedServices, MOIForms, MOAForms, PackageScheduleItems, DivisionGroups, DivisionGroupRecommenders, FormTemplates, WorkflowTemplates, WorkflowStepTemplates, WorkflowInstances, WorkflowStepInstances, ServiceJobForms, BillingParties, JobItemDocuments, SignatoryCustomerAccess, AppNotifications, Invoices, PasswordResetOtps, plus `__EFMigrationsHistory`.

---

## 6. The three provider type gotchas (this is where data silently corrupts — read carefully)

### 6.1 DateTime / UTC — the biggest risk (42 DateTime columns)
Npgsql maps `DateTime` to `timestamp with time zone` and **throws at runtime if you write a `DateTime` whose `Kind` is not `Utc`** ("Cannot write DateTime with Kind=Unspecified…"). The codebase mostly uses `DateTime.UtcNow` (good), but values **read back from the DB and re-saved**, or parsed from strings, are often `Kind=Unspecified`. This is the same class of bug that caused the concurrency false-conflict fixed in Review #3 (F1).

Do this:
1. Add a global UTC convention so every `DateTime`/`DateTime?` is treated as UTC on read and write. In `AppDbContext.OnModelCreating`, after the entity config, add a value converter for Postgres:
```csharp
// Only needed for Postgres, but harmless elsewhere: force UTC kind on all DateTime columns.
if (Database.IsNpgsql())
{
    var toUtc = new ValueConverter<DateTime, DateTime>(
        v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
    var toUtcN = new ValueConverter<DateTime?, DateTime?>(
        v => v.HasValue ? (v.Value.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc)) : v,
        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
    foreach (var et in modelBuilder.Model.GetEntityTypes())
        foreach (var p in et.GetProperties())
            if (p.ClrType == typeof(DateTime)) p.SetValueConverter(toUtc);
            else if (p.ClrType == typeof(DateTime?)) p.SetValueConverter(toUtcN);
}
```
`Database.IsNpgsql()` requires `using Microsoft.EntityFrameworkCore;` and the Npgsql package. Note: `OnModelCreating` cannot always read the provider reliably; if `Database.IsNpgsql()` is not available there, gate on the injected provider name instead (pass a flag into the context, or apply the converter unconditionally — the converter is safe for SQLite/SqlServer too).
2. Keep the Review #3 `FormConcurrencyHelper` fix (symmetric `SpecifyKind(Utc)`), which is already in place.

**Acceptance**: after migrating data (§8), log in and open an MOA form, edit and save it — no `Kind=Unspecified` exception, save returns 200 with a correct `expectedUpdatedAt` round-trip. Test a `forgot-password` OTP (uses `CreatedAt`/`ExpiresAt` DateTimes).

### 6.2 Guid — `ConcurrencyStamp`
Postgres has a native `uuid` type; Npgsql maps `Guid` to it automatically. No code change needed, but note the SQLite hand-migrator stored it as TEXT — that column definition is irrelevant on Postgres (the migration in §5 creates it as `uuid`). When copying data (§8), the Guid string values convert cleanly.

### 6.3 JSON blob columns and identity keys
- The `*Json` columns (`FormDataJson`, `PricingJson`, `ClientApprovalsJson`, `InvoiceByPartyIdsJson`, `MoiJson`, etc.) are plain **string** properties. On Postgres they become `text` — **keep them as `text`**. Do *not* switch them to `jsonb` in this migration; the app reads/writes them as serialized strings and jsonb would require code changes and risks escaping bugs. (Optional future enhancement, out of scope here.)
- Integer primary keys are `INTEGER … AUTOINCREMENT` on SQLite; the Postgres migration makes them `integer … GENERATED BY DEFAULT AS IDENTITY` (or serial). After copying data with explicit ids (§8), you **must reset the identity sequences** or the next insert collides — §8 step 4.

---

## 7. Confirm SqliteSchemaMigrator is inert on Postgres
Already gated in `DatabaseBootstrap` (§4). Double-check no other caller runs it unconditionally: `grep -rn "SqliteSchemaMigrator" LGBApp.Backend --include=*.cs | grep -v Migrations`. Only `DatabaseBootstrap` should call `.Apply`, behind `IsSqlite()`.

**Acceptance**: grep shows the single guarded call site.

---

## 8. Copy existing data SQLite → Postgres (preserve every row and relationship)

The schema is now on Postgres but empty. Copy the real data. Use **pgloader** — it handles type coercion (TEXT→timestamp, TEXT→uuid, INTEGER→bool) automatically and preserves foreign-key data.

1. Install pgloader (`brew install pgloader` / `apt-get install pgloader`).
2. Write `migrate.load`:
```
load database
    from sqlite:///absolute/path/to/lgbapp.db
    into postgresql://postgres:lgb@localhost:5432/lgbapp

 with data only, drop indexes, reset sequences, quote identifiers
 set work_mem to '128MB', maintenance_work_mem to '512MB'
 before load do $$ set session_replication_role = replica; $$   -- defer FK checks during copy
 after  load do $$ set session_replication_role = default; $$;
```
   - `data only` — keep the EF-created schema (§5); only copy rows. **Do not let pgloader create the schema** — EF owns it, so the relationships/indexes match the model exactly.
   - `quote identifiers` — the tables are PascalCase (`"Users"`, `"JobRequests"`); Postgres folds unquoted names to lowercase, so quoting is required to match EF's expectations.
   - `reset sequences` — fixes identity counters (also do the explicit check in step 4).
   - `session_replication_role = replica` — lets rows load in any order without tripping FK constraints mid-copy; restored to `default` after.
3. Run: `pgloader migrate.load`. Read the summary — row counts per table must be > 0 for the tables that had data.

> **Pre-load gotcha (schema newer than data):** if the SQLite file predates columns like `WorkflowMode` / `ConcurrencyStamp` / `AdminBypassNote`, those source columns are absent and Postgres copies `NULL` into `NOT NULL` columns → `COPY` fails. Before loading:
> ```sql
> ALTER TABLE "JobRequests" ALTER COLUMN "WorkflowMode" DROP NOT NULL;
> ALTER TABLE "JobRequests" ALTER COLUMN "AdminBypassNote" DROP NOT NULL;
> ALTER TABLE "JobRequestUnits" ALTER COLUMN "WorkflowMode" DROP NOT NULL;
> ALTER TABLE "JobRequestUnits" ALTER COLUMN "AdminBypassNote" DROP NOT NULL;
> ALTER TABLE "MOIForms" ALTER COLUMN "ConcurrencyStamp" DROP NOT NULL;
> ALTER TABLE "MOAForms" ALTER COLUMN "ConcurrencyStamp" DROP NOT NULL;
> ```
> After a clean load, backfill and restore `NOT NULL`:
> ```sql
> UPDATE "JobRequests" SET "WorkflowMode" = '' WHERE "WorkflowMode" IS NULL;
> UPDATE "JobRequests" SET "AdminBypassNote" = '' WHERE "AdminBypassNote" IS NULL;
> UPDATE "JobRequestUnits" SET "WorkflowMode" = '' WHERE "WorkflowMode" IS NULL;
> UPDATE "JobRequestUnits" SET "AdminBypassNote" = '' WHERE "AdminBypassNote" IS NULL;
> UPDATE "MOIForms" SET "ConcurrencyStamp" = gen_random_uuid() WHERE "ConcurrencyStamp" IS NULL;
> UPDATE "MOAForms" SET "ConcurrencyStamp" = gen_random_uuid() WHERE "ConcurrencyStamp" IS NULL;
> -- then ALTER … SET NOT NULL + SET DEFAULT as needed
> ```
>
> **Docker pgloader (no local brew install):** share the Postgres container network and mount the SQLite file:
> ```bash
> docker run --rm --platform linux/amd64 --network container:lgb-pg \
>   -v /absolute/path/lgbapp.db:/data/lgbapp.db:ro \
>   -v "$PWD/docs/deploy/migrate.load.example:/migrate.load:ro" \
>   dimitri/pgloader:latest pgloader /migrate.load
> ```
> (Adjust the `into postgresql://…` host in the load file to `127.0.0.1:5432` when using `--network container:…`.)

4. **Reset identity sequences** (critical — otherwise the first new insert PK-collides):
```sql
-- run in psql against lgbapp; repeat pattern for EVERY table with an int identity PK
SELECT setval(pg_get_serial_sequence('"Users"','UserId'),        COALESCE((SELECT MAX("UserId") FROM "Users"), 1));
SELECT setval(pg_get_serial_sequence('"Customers"','CustomerId'), COALESCE((SELECT MAX("CustomerId") FROM "Customers"), 1));
SELECT setval(pg_get_serial_sequence('"JobRequests"','JobRequestId'), COALESCE((SELECT MAX("JobRequestId") FROM "JobRequests"), 1));
-- …and so on for JobRequestUnits, JobRequestUnitAssignees, MOIForms, MOAForms, CustomerPackages,
--    AccountHolders, PackageScheduleItems, CompletedServices, DivisionGroups, DivisionGroupRecommenders,
--    FormTemplates, WorkflowTemplates, WorkflowStepTemplates, WorkflowInstances, WorkflowStepInstances,
--    ServiceJobForms, BillingParties, JobItemDocuments, SignatoryCustomerAccess, AppNotifications,
--    Invoices, PasswordResetOtps, Products.
```

**Acceptance (do all of these)**:
- Row counts match between SQLite and Postgres for every table:
  ```bash
  for t in Users Customers JobRequests JobRequestUnits MOIForms MOAForms AccountHolders CustomerPackages; do
    echo -n "$t sqlite="; sqlite3 lgbapp.db "SELECT COUNT(*) FROM \"$t\";" | tr -d '\n'
    echo -n " pg=";      psql "$PGURL" -tAc "SELECT COUNT(*) FROM \"$t\";"
  done
  ```
- Foreign keys resolve: no orphans. Spot check e.g. every `JobRequestUnits.JobRequestId` exists in `JobRequests`:
  ```sql
  SELECT COUNT(*) FROM "JobRequestUnits" u LEFT JOIN "JobRequests" j ON j."JobRequestId"=u."JobRequestId" WHERE j."JobRequestId" IS NULL;  -- must be 0
  ```
- A brand-new insert works without PK collision (create a test customer via the API, then delete it).

> **Do not** run the `SEED_FULL` bootstrap against a Postgres DB that already has copied data — it is for empty databases. The idempotent light seeders (staff/catalog/reference) are safe and will no-op on existing rows.

---

## 9. Migrate the uploaded files (separate from the DB!)
The DB only stores `JobItemDocument.StorageKey` (a relative path). The bytes are on disk under `LGB_UPLOAD_ROOT`. After the DB is on Postgres, the app still reads files from that path.
- **If staying on the same host/volume**: point `LGB_UPLOAD_ROOT` at the same directory that holds the existing files (production keeps `/data/uploads`). Nothing to copy — just don't lose the volume.
- **If moving hosts**: copy the entire uploads tree to the new host's `LGB_UPLOAD_ROOT`, preserving the relative subpaths exactly (`{jobId}/{folder}/{guid}_{filename}`). A single wrong path prefix makes every download 404.
- **For scale ("a lot a lot of files")**: strongly consider object storage (S3/R2) via the `IDocumentStorage` extension from Review #1 (E7). Postgres does not store these files and should not — do not try to load files into the DB.

**Acceptance**: pick 3 known documents, hit `GET /api/jobs/{jobId}/documents/{docId}/download` for each — all return the file, not 404.

---

## 10. Full verification checklist (all must pass before switching production)

1. `dotnet build` — 0 errors. `dotnet test LGBApp.Backend.Tests` — all green (73+).
2. Boot the app against Postgres (`Database__Provider=Postgres`, Postgres connection string). Startup log shows migrations applied and seeders run; `/api/health` returns ok.
3. **Relationships intact** — run in psql, expect a healthy count of FKs (the model defines ~25):
   ```sql
   SELECT conrelid::regclass AS table, conname FROM pg_constraint WHERE contype='f' ORDER BY 1;
   ```
   Confirm the key ones exist: JobRequests→Customers, JobRequests→CustomerPackages, JobRequestUnits→JobRequests, JobRequestUnitAssignees→(JobRequestUnits,Users), AccountHolders→Customers, CustomerPackages→Customers, MOIForms→(JobRequests,JobRequestUnits), MOAForms→(JobRequests,JobRequestUnits,MOIForms), WorkflowInstances→(MOIForms,MOAForms,WorkflowTemplates), SignatoryCustomerAccess→(Users,Customers).
4. **Login works** with a real migrated user (proves password hashes copied intact — BCrypt strings are ASCII, copy cleanly).
5. **End-to-end smoke** (mirror the review simulations): create a customer → client issues MOI → sign-off with signature → intake → assign staff → MOA pack save (confirms the DateTime/UTC fix) → sharon approve → client MOA sign-off → execution complete. No `Kind=Unspecified` errors anywhere.
6. **Concurrency**: two parallel MOA `pack` saves → one 200, one 409 (proves the row-version/`ConcurrencyStamp` works on Postgres).
7. **Files**: 3 document downloads succeed (§9).
8. Identity sequences correct: create+delete one row in each of Users/Customers/JobRequests — no PK collision.

---

## 11. Production cutover & rollback

- **Cutover**: put the app in maintenance (stop writes) → take a final SQLite backup → run §8 copy against the production Postgres → run §10 checks → flip `Database__Provider=Postgres` + `ConnectionStrings__DefaultConnection` (Postgres URL) → restart → smoke test. Keep `LGB_UPLOAD_ROOT` pointed at the existing files (§9).
- **Managed Postgres**: use a managed instance (Railway/Neon/RDS). Set `sslmode=require` in the connection string. Ensure `TimeZone` handling is UTC end-to-end (the value converter in §6.1 makes the app UTC-safe regardless of server tz).
- **Rollback**: because you only *read* from SQLite during the copy and the SQLite DB is untouched, rollback is: flip `Database__Provider` back to `Sqlite` and restore the original connection string. The SQLite file and uploads are exactly as before. Keep the SQLite path working (do not delete `SqliteSchemaMigrator` or the SQLite provider) until Postgres has run clean in production for at least one full cycle.

---

## 12. Summary of files you will touch
- `LGBApp.Backend/LGBApp.Backend.csproj` — add Npgsql package.
- `LGBApp.Backend/Program.cs` — add Postgres branch to DbContext registration + the startup migrate/seed branch.
- `LGBApp.Backend/Data/DevDatabaseReset.cs` — Postgres reset path.
- `LGBApp.Backend/Data/DesignTimeDbContextFactory.cs` — provider-aware for `dotnet ef`.
- `LGBApp.Backend/Data/AppDbContext.cs` — add the UTC DateTime value-converter (§6.1). **Do not touch the relationship config or the concurrency-stamp logic.**
- `Migrations/Postgres/*` — new, generated by EF (§5). Never hand-edit.
- No changes to controllers, services, models, or seeders — they are provider-agnostic.

**Non-goals (do not do these here)**: converting JSON columns to jsonb; removing SQLite/SqlServer support; changing any relationship, cascade rule, or seeder; moving files into the database.
