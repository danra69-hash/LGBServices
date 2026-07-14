# LGBServices — System Review #2: Live Role Simulation & Interaction Edge Cases

Date: 2026-07-14. Method: **the backend was booted against a scratch SQLite DB and driven end-to-end through the real HTTP API as four concurrent role sessions** — Admin (Sharon), internal prep staff (Nita), ClientAdmin (Acme base admin), and ClientSignatory (Alice + Bob) — plus a second tenant (Globex/Carl) for isolation tests. Every finding below was reproduced against the running binary, not inferred from source.

This document is for an implementing model. Findings are grouped by whether they are **already fixed** (do not touch), **newly found** (build these), or **design questions** (confirm intent before coding). Each new finding has a reproduction, a root cause with `file:line`, a fix spec, and acceptance criteria.

---

## 0. Important context: the codebase moved since Review #1

`git log` shows Review #1's entire plan (Waves 1–5) was implemented between the two reviews:
```
c62829d Ship Wave 5 schema, runtime, and query hardening.
b078b52 Consolidate schema onto EF Migrate for SQLite production.
9a6507f Fix Wave 3 bugs: email, history, upload, and MOI recommend gates.
7372d9c Add RFC7807 exception handling and standardize API errors.
a97d3d0 Fix Wave 1 critical security issues from system review.
```
Test count grew from ~30 to **62** methods. I re-verified the Wave 1 criticals live (see §1). **Do not re-implement Review #1 items** — they are done. This review is net-new.

---

## 1. Review #1 items — verified FIXED via live testing (do not touch)

| Ref | What I tested live | Result |
|-----|--------------------|--------|
| C1 | ClientSignatory (Alice) and ClientAdmin `PUT /api/jobrequests/{id}` with overwritten fields + `Status=Completed` | **403** both. Admin-only guard + status allowlist + `TotalQty≥1`/`TotalQty≥UsedQty` validation confirmed in `JobRequestsController.cs:611`. |
| C2 | JWT placeholder/short-key guard + `EnableRateLimiting("auth")` on login | Present in `Program.cs`; staff seeding gated behind `IsDevelopment() || SEED_STAFF=true` with required `SEED_STAFF_PASSWORD`. |
| C3 | Upload root | `ENV LGB_UPLOAD_ROOT=/data/uploads` in Dockerfile (survives redeploy). |
| C4 | Sharon `POST /moiforms/{id}/approve` on a **Draft** MOI | **400** "MOI cannot be approved from state 'Draft'…". State gate live. |
| C5 | Invoice creation | Retry loop `for attempt<3` catching `DbUpdateException`, `NextInvoiceNumberAsync` computes max suffix for today's prefix. |
| C6 | Multi-company signatory job filter | `GetJobRequests` now filters external users by `GetAccessibleCustomerIds` (`JobRequestsController.cs:51`). |
| §5 | Error shape | Live 429 returned RFC7807 ProblemDetails (`{type,title,status,detail}`) — exception handler is wired. |
| §6 | DB consolidation | Both providers boot via EF `Migrate()` (`DatabaseBootstrap.ApplyMigrations`); two real migrations exist (`Baseline_FullSchema`, `Wave5_FormCustomerIds_Indexes_InvoiceFks`); hand migrator runs only for legacy coexistence. |
| S3 | Complete → revert → re-complete a job | `CompletedService.JobRequestId` now stamped; revert removed exactly 1 row, re-complete did not duplicate (count stayed correct). |
| S9 | Frontend dead code / error boundary | `supabase.ts` gone, `@supabase/*` removed from package.json, `ErrorBoundary.tsx` added and mounted in `main.tsx`. |

### Isolation & privilege controls — verified SOLID (do not "harden" these; they work)
- **Cross-tenant reads/writes all blocked**: Globex's Carl got 403 reading Acme's MOI, 403 on Acme docs, 403 issuing MOI on an Acme job; Acme's ClientAdmin got 403 on `/api/customers/2`. Each role sees only its own company's jobs.
- **Multi-signer sign-off**: MOI `AllRequired` mode correctly waited for both Alice and Bob; double-approval returned 400 "already approved."
- **Partial multi-session release**: on a qty-5 job, issuing+approving only session 1 exposed *only* session 1 to internal staff; sessions 2–5 stayed hidden.
- **User-management escalation blocked**: ClientAdmin creating an internal `Admin` → 403; `customerId` override to another tenant → silently forced back to own customer; `canApproveMoa/canApproveMoiIntake` on an external role → stripped to false.
- **Intake authorization**: non-approver internal staff (Nita) got 403 on `approve-intake`; OTP is enumeration-safe (200 for known+unknown) with a working 429 cooldown.

---

## 2. NEW findings from the simulation (build these)

### N1 — Reverting a completed execution leaves `InternalHandoffStatus` stuck at `Completed` (Medium)
- **Repro**: drive a job to `PendingExecute` → `progress {markUnitComplete}` (handoff→`Completed`, unit→`Completed`) → `progress {markUnitIncomplete}`. Result: `unit.Status = "In Progress"` but `unit.InternalHandoffStatus = "Completed"` and `job.Status = "In Progress"`. The unit is simultaneously "in progress" and "completed handoff."
- **Root cause**: `Services/JobRequestUnitService.cs:155` `RevertUnitCompleteAsync` resets `Status` and `CompletedAt` but never rolls back `InternalHandoffStatus`. The MOA-driven completion set it to `Completed` (`JobHandoffService.cs:478` `OnExecutionCompletedAsync` → `MirrorJobHandoff(...Completed)`), and nothing undoes it.
- **Impact**: `PackageItemStatusResolver`/`displayStatus` now report a contradictory state; re-completion falls through the *plain-complete* branch instead of the execution branch (it happens to still work, but for the wrong reason). Any future logic keying on handoff will misfire.
- **Fix spec**: in `RevertUnitCompleteAsync`, when the effective handoff is `Completed`, roll it back to `PendingExecute` (the state that precedes execution completion) via `JobHandoffResolver.MirrorJobHandoff(job, unit, JobHandoffStatuses.PendingExecute)`; then run `SyncJobHandoffFromUnits`. Only do this when the unit came from the MOA execution path (handoff was `Completed`); for plain-complete units (handoff empty) keep current behavior.
- **Accept when**: revert of an executed unit yields `unit.InternalHandoffStatus == "PendingExecute"`; a new test asserts complete→revert→state is `PendingExecute`, and re-complete goes through `OnExecutionCompletedAsync` again.

### N2 — Optimistic-concurrency guard silently loses updates within a 1-second window (Medium)
- **Repro**: read a MOA form's `updatedAt`, then fire two `PUT /api/moaforms/{id}/pack` in parallel both carrying that same `expectedUpdatedAt`. **Both returned 200**; the second silently overwrote the first (last-writer-wins) with no conflict.
- **Root cause**: `Services/FormConcurrencyHelper.cs:24-26` treats deltas `<= 1` second as "no conflict," and the check is non-transactional (read-compare-write with no DB guard). Two writes that interleave before either commits both see the unchanged timestamp and both pass. There is no `[ConcurrencyCheck]`/`rowversion` on `MOIForm`/`MOAForm`.
- **Impact**: two staff (or a staff + an auto-save) editing the same MOA pack/MOI within the same second silently clobber each other. On collaborative forms this is real data loss, and it is invisible — no error, no audit.
- **Fix spec**:
  1. Add a concurrency token to `MOIForm` and `MOAForm`. For SQLite/EF, add a `RowVersion` column mapped with `.IsRowVersion()` (or a `Guid ConcurrencyStamp` reset on every write with `.IsConcurrencyToken()`), via a new migration + hand-migrator column.
  2. In the pack/update handlers, catch `DbUpdateConcurrencyException` and return the existing 409 body (`{message:"This form was updated by someone else…", updatedAt}`).
  3. Remove the `<= 1` second tolerance in `FormConcurrencyHelper` (keep it only as a cheap pre-check; the DB token is the real guard).
- **Accept when**: the two-parallel-save repro yields exactly one 200 and one 409; single-user save still works.

### N3 — Unassigning the last prep staff on an in-prep session orphans the work (Medium)
- **Repro**: approve intake on a session → assign Nita → remove Nita. Result: `unit.Status = "In Progress"`, `unit.InternalHandoffStatus = "PendingPrep"`, an MOA shell exists, but **no assignees**. The session now appears in *no one's* `my-tracker` (that endpoint filters by assignee, `JobRequestsController.cs:96-98`) and is not surfaced as needing assignment.
- **Root cause**: `JobRequestUnitService.RemoveAssigneeAsync` only reverts `Status` to `Pending` when the unit is still `In Progress` *and* has no assignee — but once the handoff advanced to `PendingPrep`, nothing resets the handoff, so the unit stays "mid-prep" with nobody on it. It falls out of every assignee-scoped view.
- **Impact**: silently stalled work. Only an Admin scanning all jobs finds it; the prep queue loses it.
- **Fix spec**: when removing the last assignee from a unit whose handoff is in a prep phase (`PendingPrep`/`ResoInProgress`) and not yet submitted for review, roll the unit handoff back to `AwaitingSecAssignment` (the "needs a team" state) and set unit `Status = "Pending"`. This puts it back on the assignment radar. Guard: do not roll back if the MOA was already submitted for admin review (`SubmittedForAdminReviewAt != null`).
- **Accept when**: after removing the last assignee mid-prep, the unit shows `AwaitingSecAssignment` and appears wherever unassigned-but-released work is listed (Sharon's assignment view).

### N4 — Cross-tenant form endpoints check state before authorization, leaking existence/phase (Low)
- **Repro**: Carl (Globex) `POST /api/moaforms/1/client-approve` on an Acme form returned **400** ("MOA is not available for client sign-off") rather than 403/404 — the handoff-state check (`MOAFormsController.cs:214`) runs *before* the customer-access check (`:222`).
- **Impact**: a cross-tenant caller can distinguish "form exists and is/ isn't in sign-off phase" from "form doesn't exist," an IDOR oracle. Low severity (no data returned) but it discloses the workflow state of another tenant's forms.
- **Fix spec**: in every MOA/MOI action that resolves a `customer`, move the `CanAccessCustomer`/tenant check to immediately after loading the form (before any state/phase validation). Return 404 (not 403) for forms the caller can't access, so existence isn't disclosed. Apply to `client-approve`, `client-reject`, `submit-for-approval`, `recommend`.
- **Accept when**: Carl's call on an Acme form returns 404; Acme signatory's legitimate call still works.

---

## 3. Design questions — confirm intent before changing (may be correct as-is)

These are behaviors the simulation surfaced that are *plausibly intentional*. Do not "fix" them without the owner confirming; each has a real business decision behind it.

- **D1 — Client can mark plain `Service` jobs complete with no MOI/MOA at all.** ClientAdmin `POST /clientjobs/1/progress {markUnitComplete}` on "Annual Return" (which never went through any workflow) completed it and wrote a `CompletedService` row. This looks intended for non-resolution services, but it means those lines skip all MOI/MOA governance. **Confirm**: which service types (by product/service name) must be forced through the workflow vs. may be client-closed directly? If a list exists, enforce it server-side in both `progress` endpoints.
- **D2 — Only `CanApproveMoa`/Admin can mark execution complete; the assigned prep staff cannot.** Nita, who did all the MOA prep, got 403 closing the executed unit (`JobRequestsController.cs:556`); only Sharon could. If intended (final close is a controlled act), fine — but it's a throughput bottleneck on Sharon. **Confirm** whether assigned staff should be able to mark *execution* done while approval stays separate.
- **D3 — MOA/MOI client sign-off is accepted with no signature.** `client-approve` accepts a null `SignatureDataUrl`/`SignatureFileName` and records the approval. For legally-operative resolutions this may need a required signature artifact. **Confirm** whether signature capture is mandatory; if so, reject empty in both `client-approve` handlers.
- **D4 — Internal signatory sign-off matches by display name.** `ClientApprovalService.ResolveMoaSignerName` returns an internal user's `Name` for anyone with `CanApproveMoa`/`IsInternalSignatory`. If an internal user's name equals a *required client MOA holder's* name, the internal countersignature could satisfy the client requirement (`MoaClientPhaseComplete` matches by name, case-insensitive). Rare, but name-based matching is fragile. **Confirm** whether internal and client signer namespaces should be kept separate (e.g., prefix internal records, or match required client signers by `UserId`/holder id instead of name).

---

## 4. Carry-over items from Review #1 still worth doing (not yet addressed)

These weren't part of Waves 1–5 and remain open (lower priority than §2):
- **A4** — `MustChangePasswordMiddleware` path allowlist uses `StartsWith` (prefix match). Compare exact segments.
- **A5 / E5** — 7-day JWT with no revocation; deleted/demoted users keep access until expiry. Refresh-token module still open.
- **EF3** — list endpoints (`customers`, `completedservices`, `my-jobs`) remain unpaginated.
- **§7.2** — multi-`SaveChanges` controller actions (`CustomersController.Create/Update`, `JobRequestsController.assign/handoff`) still lack wrapping transactions; a mid-sequence failure leaves partial state. The simulation didn't crash any, but the risk is unchanged.
- **§7.5** — `CustomersController.UpdateCustomer` still deletes+recreates all `AccountHolders` each save (`:101-119`), churning ids that signatory provisioning and dedup reference. The provisioner re-links by email so it survives, but it's wasteful and id-unstable.

---

## 5. Execution order for the implementing model

1. **N2 (concurrency token)** first — it's silent data loss and needs a migration, so land it before other form changes.
2. **N1, N3** — state-machine rollback bugs; one commit + one test each. Re-run `python3 scripts/workflow_backtest.py` against a running API after each (both touch `JobHandoffService`/`JobRequestUnitService`).
3. **N4** — authorization-ordering; small, mechanical, apply to all four form actions together.
4. **§3 design questions** — take to the owner; only code once intent is confirmed. Do not guess.
5. **§4 carry-overs** — as capacity allows, in the order listed.

### Guardrails (unchanged from Review #1, re-confirmed live)
- Run `dotnet test LGBApp.Backend.Tests` after every commit (62 methods must stay green) and the Python `workflow_backtest.py` after any change to `JobHandoffService`, `JobHandoffResolver`, `JobRequestUnitService`, or the MOI/MOA/JobRequests controllers.
- Any new column (N2's row-version) goes in **both** an EF migration and `SqliteSchemaMigrator.EnsureColumn` while the hand migrator still coexists (`DatabaseBootstrap` runs both).
- Never rename status/handoff/workflow-state string literals — they are compared verbatim across backend and `src/lib/packageItemStatus.ts`.
- Preserve the `{ message, errors? }` / RFC7807 error shapes the frontend parses.
- When a fix conflicts with `workflow_backtest.py`, the backtest wins — adjust the fix, not the workflow.
