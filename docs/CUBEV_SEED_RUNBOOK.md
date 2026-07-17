# Runbook — Initialise the CubeV customer data into a live LGBServices database

Audience: an agent executing this against a target PostgreSQL database (production/Railway). Follow the steps in order. Each step has an **acceptance check** — do not proceed until it passes. This has been dry-run verified end-to-end against a fresh Postgres (~2.5 min, produces the counts in Step 5).

---

## 0. What this does and the one gotcha

Loads the full CubeV book — **169 customers, their packages, ~1,079 jobs, client logins, signatories, billing parties** — from `LGBApp.Backend/Data/Seed/cubev-init.json` (built from `docs/source/COSEC_Billing_Tracking_2026_CubeV.xlsx`).

**GOTCHA — two separate seed steps, don't conflate them:**
- **Internal staff / the Admin account (Sharon)** are created on a **normal app boot** only when `SEED_STAFF=true` + `SEED_STAFF_PASSWORD` are set. `seed-full` does **NOT** create them.
- **CubeV customers + jobs + client logins** come from the **`seed-full` CLI command**.
- The old `SEED_FULL=true` env var is **ignored** now (it printed a message and skipped, for healthcheck safety). The **only** way to load CubeV is the CLI command in Step 4.

So a fresh database needs **both**: staff seeded (Step 3) and CubeV seeded (Step 4). If the target is "already live" and Sharon can already log in, Step 3 is already done — verify and skip it.

---

## 1. Preconditions

- The repo is checked out and `dotnet` (SDK 8.0) is available. Confirm: `dotnet --version` → `8.0.x`.
- The seed file exists: `LGBApp.Backend/Data/Seed/cubev-init.json` (169 companies). Confirm: it is present and non-empty.
- You have the target database's connection string and it is a **PostgreSQL** instance the app's migrations have (or will) run against.
- **Environment variables** for every command below (export them once):
  ```bash
  export ASPNETCORE_ENVIRONMENT=Production
  export Database__Provider=Postgres
  export ConnectionStrings__DefaultConnection="Host=<host>;Port=<port>;Database=<db>;Username=<user>;Password=<pass>;SSL Mode=Require;Trust Server Certificate=true"
  ```
  (On Railway, run these as a one-off command in the API service so `DATABASE_URL`/service vars resolve; convert `postgres://user:pass@host:port/db` to the `Host=…;Port=…;…` form above if the app expects key-value.)

**Acceptance**: `dotnet --version` is 8.0.x; the seed JSON is present; the three env vars are set and point at the intended database.

---

## 2. Back up first (non-negotiable)

Take a logical dump of the target DB before writing anything.
```bash
pg_dump "<same connection, libpq URL form>" -Fc -f ~/lgb-preseed-$(date +%Y%m%d-%H%M).dump
```
**Acceptance**: the dump file exists and is > 0 bytes. If you cannot dump (no `pg_dump`/network), STOP and get someone who can — do not seed an un-backed-up production DB.

---

## 3. Ensure internal staff / Admin exist (skip if already live with a working Sharon login)

Check first:
```sql
SELECT "Email","Role" FROM "Users" WHERE "CustomerId" IS NULL ORDER BY "UserId";
```
- If `sharon@lgb.test` (Role `Admin`) is present → **staff already seeded, skip to Step 4.**
- If **no** internal-staff rows → seed them by booting the app **once** with staff seeding enabled:
  ```bash
  export SEED_STAFF=true
  export SEED_STAFF_PASSWORD="<choose a strong initial password>"   # REQUIRED in non-Development; boot throws without it
  dotnet run --project LGBApp.Backend --no-launch-profile
  # wait for "[Startup] Light seed complete (staff + catalog)…", then stop the process (Ctrl-C)
  unset SEED_STAFF SEED_STAFF_PASSWORD
  ```
  This also applies EF migrations and seeds the workflow config + product catalog. Seeded staff get `MustChangePassword=true`, so they must change the password on first login.

**Acceptance**: the `Users` query shows `sharon@lgb.test / Admin` plus the resolution staff (`ngpohli`, `nita`, `siti`, `nadia`).

---

## 4. Run the CubeV seed (the main step)

One self-contained command. It migrates, seeds workflow config, imports CubeV customers, provisions client logins + signatories + billing parties, and bootstraps jobs. It is **idempotent** — safe to re-run; it skips anything already present (`CubeVCustomerSeeder.SeedIfNeeded` no-ops when customers exist, and job bootstrap only runs on an empty-jobs DB).

```bash
dotnet run --project LGBApp.Backend --no-launch-profile -- seed-full
```

Expected console tail: `[seed-full] Bootstrapping job requests…` then `[seed-full] Job bootstrap complete — 1079 jobs.` then `[seed-full] Done.` Runtime ≈ 2–3 minutes.

**Acceptance**: process exits 0 and prints `[seed-full] Done.` (If it errors partway, see Step 6.)

> Railway note: run this as a **one-off command / job** on the API service (not the web process), because it takes minutes and must not be on the healthcheck path. The command exits when finished; it does not start the web server.

---

## 5. Verify the result (must match)

```sql
SELECT
  (SELECT count(*) FROM "Customers")                                   AS customers,        -- 169
  (SELECT count(*) FROM "CustomerPackages")                            AS packages,         -- 169
  (SELECT count(*) FROM "JobRequests")                                 AS jobs,             -- 1079
  (SELECT count(DISTINCT "CustomerId") FROM "JobRequests")             AS customers_with_jobs, -- 113
  (SELECT count(*) FROM "Users" WHERE "Role"='ClientAdmin')            AS client_admins,    -- 169
  (SELECT count(*) FROM "Users" WHERE "Role"='ClientSignatory')        AS signatories,      -- 90
  (SELECT count(*) FROM "BillingParties")                              AS billing_parties,  -- 174
  (SELECT count(*) FROM "DivisionGroups")                              AS division_groups;  -- 18
```

**Acceptance**: `customers=169`, `packages=169`, `jobs=1079`, `client_admins=169`. (Signatories/billing-parties can vary slightly if the target already had some rows; customers and jobs are the load-bearing numbers.) Then smoke-test: log in as `sharon@lgb.test` and confirm the customer list is populated.

### Known, expected: 56 customers will have NO jobs
```sql
SELECT count(*) FROM "Customers" c
WHERE NOT EXISTS (SELECT 1 FROM "JobRequests" j WHERE j."CustomerId"=c."CustomerId");  -- 56
```
This is **not a failure**. All 56 are "Add-ons only" packages whose source rows in `cubev-init.json` have `addOns: []` and `resoQty: 0` — a lump-sum value (RM 600/1440) with no itemised services, so there is nothing to turn into work. They load correctly as companies + packages; they just have no scheduled jobs. **Do not try to "fix" this by inventing jobs.** If the owner wants those 56 to have work, they must supply the add-on line items (see the note in Step 7); that is a data change, not part of this seed.

---

## 6. If it fails partway

The seed writes in stages and is re-runnable. If Step 4 errors:
1. Read the error. Common causes: bad connection string, DB not reachable, migrations not applicable (a pre-existing non-EF schema), or `cubev-init.json` not found next to the binary (it must be under `Data/Seed/` in the build output — `dotnet run` copies it).
2. Fix the cause and **re-run the same `seed-full` command** — it resumes safely (idempotent guards skip completed work).
3. If the database is left in a bad partial state and you cannot reconcile it, restore the Step 2 backup and start over.

---

## 7. What NOT to do
- Do **not** set `SEED_FULL=true` and expect a boot to load data — that env is intentionally ignored.
- Do **not** hand-edit `Migrations/`, or the seed JSON's structure.
- Do **not** invent jobs/add-ons for the 56 empty "Add-ons only" companies. If the owner provides the add-on breakdown (service name + qty per company; note the add-on unit price is RM 120, so 600 = 5 units, 1440 = 12 units), the fix is: add those `addOns` entries to the matching rows in `LGBApp.Backend/Data/Seed/cubev-init.json`, redeploy, and run `dotnet run -- seed-full` again (it will generate the new jobs). Only do this with explicit itemisation from the owner.
- Do **not** run against production without the Step 2 backup.

---

## Summary (the happy path, already-live target that just lacks CubeV)
```bash
export ASPNETCORE_ENVIRONMENT=Production Database__Provider=Postgres
export ConnectionStrings__DefaultConnection="Host=…;Port=…;Database=…;Username=…;Password=…;SSL Mode=Require;Trust Server Certificate=true"
pg_dump "<url>" -Fc -f ~/lgb-preseed-$(date +%Y%m%d-%H%M).dump      # 2. backup
# 3. verify Sharon exists (SELECT … FROM "Users" WHERE "CustomerId" IS NULL); seed staff only if missing
dotnet run --project LGBApp.Backend --no-launch-profile -- seed-full # 4. load CubeV  (~2.5 min)
# 5. verify counts: customers=169, jobs=1079, client_admins=169
```
