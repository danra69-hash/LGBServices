# LGBServices — System Review #4: Post-Migration (PostgreSQL) & Workflow Under Concurrency

Date: 2026-07-15. Method: booted the backend against a **real PostgreSQL 16** instance (`Database__Provider=Postgres`, isolated `lgbsim` database, real `lgbapp` data untouched) and drove the full MOI→MOA workflow plus concurrency races through the live HTTP API as four roles (Admin/Sharon, staff/Nita, ClientAdmin, ClientSignatory Alice+Bob). Focus: the workflow "all cases," and what **changes now that writes actually run concurrently** — SQLite serialized every write, so this is the first review where true concurrency is exercised.

**Bottom line**: the migration is structurally sound (schema, relationships, seeders, types all correct on Postgres), and every workflow path works end-to-end. But **removing SQLite's write serialization exposed one real HIGH-severity bug** (concurrent form actions return raw 500s) and sharpened three latent risks that SQLite was masking. Fix #1 before real multi-user load; the rest are ordered below.

---

## 1. Verified SOLID on PostgreSQL (don't touch — these work)

- **Schema**: 27 tables, **32 foreign keys** intact, all `DateTime` → `timestamp with time zone` (40 columns), `ConcurrencyStamp` → native `uuid`. Seeders ran (5 staff, 4 workflow templates, 3 form templates, product catalog).
- **Full lifecycle** on Postgres: issue MOI → submit → dual client sign-off *with signatures* (D3) → intake → assign staff → MOA pack save → submit-for-review → Sharon approve → release → dual client MOA sign-off → execution complete. Every step correct; job ended `Completed`.
- **Optimistic concurrency (the good path)**: the Review #3 timezone fix holds on Postgres — timestamps round-trip UTC-tagged (`…Z`), a single MOA pack save with the correct token → **200**, and a genuine parallel race on the *pack* endpoint → **one 200, one 409** (correct).
- **UTC handling**: the `AppDbContext` value-converter forcing `Kind=Utc` on all DateTime columns works — no `Kind=Unspecified` exceptions anywhere, including OTP timestamps.
- **Case-insensitive email login** works on Postgres (login `lower()` translation), despite Postgres's case-sensitive `=`.
- **Invoice number race**: 3 parallel `POST /api/invoices` → all 200, distinct numbers, no dupes — the retry loop's `DbUpdateException` catch correctly handles Postgres's unique-violation (SQLSTATE 23505).
- **Indexes**: hot columns are covered — `JobRequests(Status, CustomerId, CustomerPackageId)`, `MOIForms/MOAForms(JobRequestId, JobRequestUnitId, CustomerId, MOIFormId)`, `CompletedServices(DateCompleted, JobRequestId)`, `AppNotifications(UserId, IsRead)`. The Wave 5 indexing carried over cleanly.
- **Cross-tenant isolation, multi-session partial release, rejection→re-edit→resubmit, AdminBypass** — all still correct on Postgres.

---

## 2. HIGH — Concurrent form operations return raw HTTP 500 (fix first)

- **Reproduced**: two parallel `client-approve` on the same MOI → **one 200, one 500**. Two parallel `issue-moi` on the same job → **one 200, one 500**. Data integrity held (exactly 1 approval recorded, 1 MOI, no duplicate units — the unique indexes and the `ConcurrencyStamp` protected the data), but the losing request got an unhandled **500 Internal Server Error**.
- **Root cause**: `RotateFormConcurrencyStamps()` (`Data/AppDbContext.cs:23`) rotates the `ConcurrencyStamp` on **every** MOI/MOA `SaveChanges`, so the uuid concurrency token is active for *all* form writes. But only the `pack`/`PUT` endpoints catch the resulting `DbUpdateConcurrencyException` (via `FormConcurrencyHelper.SaveWithConcurrencyAsync`). Every **other** form-mutating endpoint — `client-approve`, `client-reject`, `submit-for-approval`, `recommend`, `approve`, the `handoff` actions, `issue-moi` (→ `OnClientMoiIssuedAsync`), and the rejection paths — calls `SaveChangesAsync` directly, so the exception is unhandled. The global handler (`Middleware/ExceptionHandlingExtensions.cs`) maps only `DomainException`; `DbUpdateConcurrencyException` and `DbUpdateException` **fall through to 500**.
- **Why it's new**: on SQLite the database serialized writes, so the second request waited and never hit a stale-stamp conflict. Postgres runs them truly concurrently. This will fire on ordinary user behavior — double-clicking "Approve," two signers acting at once, or the frontend retrying a slow request.
- **Fix (one place, covers everything)**: in `Middleware/ExceptionHandlingExtensions.cs`, before the generic 500 branch, add:
  - `DbUpdateConcurrencyException` → **409** with body `{ message: "This item was updated by someone else. Refresh and try again." }` (reuse the existing conflict shape).
  - `DbUpdateException` whose inner is a Postgres unique-violation (SQLSTATE `23505`) → **409** `{ message: "This action was already recorded." }` (idempotent-friendly; the data is already correct).
  Keep the invoice controller's own retry loop as-is. Add a `using Microsoft.EntityFrameworkCore;` and check `ex is DbUpdateException` / `ex is DbUpdateConcurrencyException`.
- **Accept when**: the two-parallel-`client-approve` repro yields one 200 and one 409 (not 500); all 73 tests still pass.

---

## 3. MEDIUM — `JobRequest` aggregate has no concurrency guard (lost-update risk)

- **Context**: `JobRequestUnitService.RefreshJobAggregateAsync` reads all units, then rewrites `JobRequest.UsedQty`, `Status`, `JobAssignedTo`, `AssignedUserId`, `ScheduledDate`. Unlike MOI/MOA forms, `JobRequest` has **no `ConcurrencyStamp`**, so concurrent writers to the same job are last-writer-wins under Postgres READ COMMITTED.
- **Tested**: two parallel `progress markUnitComplete` on units 1 & 2 of a qty-5 job → both 200, `UsedQty` ended correctly at 2 this time. But that's timing-dependent, not guaranteed: if two unit completions read the unit set before either commits, both compute the same aggregate and one overwrites the other → a completed unit that doesn't count toward `UsedQty`, or a job stuck "In Progress" when all units are done.
- **Fix (pick one)**: (a) add a `ConcurrencyStamp`/`xmin` rowversion to `JobRequest` and let the §2 handler catch it; or (b) recompute the aggregate with a `SELECT … FOR UPDATE` on the job row (EF: `context.JobRequests.FromSqlRaw("… FOR UPDATE")`) inside the transaction from §5; or (c) compute `UsedQty` as `COUNT(units WHERE Status='Completed')` in SQL rather than read-modify-write. (c) is the most robust and cheapest.
- **Accept when**: a stress test completing N units of one job in parallel always ends with `UsedQty == N`.

## 4. MEDIUM — Multi-`SaveChanges` controller actions still lack transactions (now higher-impact)

- Carried from Review #1 §7.2 and still open. `CustomersController.Create/Update`, `JobRequestsController.assign/handoff/progress`, and the MOI/MOA approve paths each call `SaveChangesAsync` several times without a wrapping transaction. On SQLite a mid-sequence failure was rare (serialized, fast). On Postgres, the §2 concurrency 500 or a transient connection drop can now abort **between** saves, leaving half-synced state (e.g., units created but aggregate not refreshed; signatory linked but job not resynced).
- **Fix**: wrap each of these actions in `await using var tx = await _context.Database.BeginTransactionAsync(); … await tx.CommitAsync();`. Start with `CustomersController` (create/update do 4+ saves) and `JobRequestsController.assign`.
- **Accept when**: killing the connection mid-action leaves the DB unchanged, not partially written.

## 5. MEDIUM — Intake rejection retains stale client signatures (workflow correctness, provider-agnostic)

- **Reproduced**: Alice+Bob sign an MOI → Sharon `reject-intake` → Alice **edits the MOI content** and `submit-for-approval` again → it jumps straight to `PendingAdminIntake` **without re-collecting signatures**. Alice's and Bob's signatures now sit on document content they never saw.
- **Root cause**: `JobHandoffService.OnMoiIntakeRejectedAsync` sets `WorkflowState=MoiRejected` but does **not** clear `ClientApprovalsJson`. Client rejection (`OnMoiClientRejectedAsync:543`) and MOA client rejection (`:643`) both do `ClientApprovalsJson = "[]"`; intake rejection does not. So on resubmit, `MoiClientPhaseComplete` sees the old approvals as still valid and skips the client sign-off phase.
- **Decide + fix**: if an intake-rejected MOI can be content-edited before resubmission (it can), clear `ClientApprovalsJson` in `OnMoiIntakeRejectedAsync` so re-signing is required — the safe, legally-defensible default. If the business wants to preserve signatures across intake bounces, instead **lock the form content** after client sign-off so only the intake issue (not the document) can change. Pick one; the current middle state (editable content + retained signatures) is the wrong one.

## 6. LOW / PERFORMANCE — In-memory filtering defeats the indexes ("fast" concern)

- The indexes in §1 are good, but the job-list path still **loads rows then filters in C#**: `JobRequestsController.GetJobRequests` fetches with `Include(Units→Assignees→User)` and applies `InternalWorkVisibilityHelper`/`TaskFormVisibilityHelper` in memory; `CompletedServices` and `my-jobs` similarly over-fetch. No index helps a query that returns everything. With "a lot of people," this is the main latency source, not the DB.
- **Fix**: push customer/status/visibility filters into the `IQueryable` before `ToListAsync` (the EF-safe `JobHandoffAwaitingIntake`/handoff-string predicates already exist for this). Add pagination (`page`/`pageSize`, cap 200) to `customers`, `completedservices`, `my-jobs`, `jobrequests`. This is the highest-leverage speed change.
- Also add **`EnableRetryOnFailure()`** to the `UseNpgsql(...)` options — managed Postgres (Railway/Neon/RDS) drops idle connections; without retry, the next query is a 500. Cheap resilience win.

---

## 7. Execution order
1. **§2** (concurrency 500 → 409) — one edit in the exception handler, protects every form endpoint. Do first.
2. **§5** (intake-reject signatures) — one line + a business decision; correctness/legal.
3. **§3** (job aggregate lost-update) — prefer the SQL `COUNT` recompute.
4. **§4** (transactions) — wrap the multi-save actions.
5. **§6** (push filters to SQL + pagination + `EnableRetryOnFailure`) — the speed pass.

### Guardrails
- Run `dotnet test LGBApp.Backend.Tests` after each change (73 must stay green). The tests use SQLite/in-memory, so they will **not** catch the concurrency-500 regression — add an integration test that fires two parallel `client-approve`s against a Postgres test container, or at minimum assert the handler maps `DbUpdateConcurrencyException`→409.
- Don't rename status/handoff/workflow-mode literals (compared verbatim across backend and `src/lib/packageItemStatus.ts`).
- The concurrency behavior differences here exist **only** on Postgres — verify these fixes against Postgres, not SQLite, or they'll look fine and still be broken in production.

---

## 8. Shipped (2026-07-16)

| # | Fix | Notes |
|---|-----|--------|
| §2 | `ExceptionHandlingExtensions.MapException` | `DbUpdateConcurrencyException` + unique (`23505` / message heuristics) → **409**. Covered by `Review4ExceptionMappingTests`. |
| §5 | `OnMoiIntakeRejectedAsync` | Clears `ClientApprovalsJson = "[]"` so re-sign is required after content edits. |
| §3 | `RefreshJobAggregateAsync` | Postgres `FOR UPDATE` when inside a transaction; `UsedQty` from SQL `COUNT` + tracker overlay. |
| §4 | `TransactionHelper` | Customers create/update + job assign/progress wrapped in execution-strategy transactions (compatible with `EnableRetryOnFailure`). |
| §6 | Retry + pagination | `UseNpgsql(... EnableRetryOnFailure())`; `page`/`pageSize` (cap 200) on customers, jobrequests, completedservices, my-jobs; completed-services assignee filter + my-jobs `TaskType` filter pushed to SQL. Internal job-list visibility still filters in memory after load (helpers not expressible in SQL yet). |

**Tests**: `dotnet test LGBApp.Backend.Tests` → **79** green (was 73). Postgres parallel-race repro still recommended on Railway after deploy.
