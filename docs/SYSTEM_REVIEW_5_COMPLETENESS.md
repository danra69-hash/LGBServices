# LGBServices — System Review #5: Completeness Scorecard & Final Consolidated Review

Date: 2026-07-16. Method: booted against **real PostgreSQL 16** (isolated `lgbsim` DB, production data untouched), re-verified every Review #4 fix live, swept all feature endpoints across the four roles, and exercised the full MOI→MOA workflow plus concurrency races. All 79 unit tests pass. This is the **closeout document** — §2 is the percentage scorecard you asked for; §3 is the single final review of everything that is *not* 100%, so you don't have to run this loop again.

---

## 1. Review #4 fixes — all verified live on Postgres (shipped, working)

| Fix | Verified this round |
|-----|---------------------|
| §2 concurrency → 409 | `issue-moi` race → **409 + 200** (was 500). Double `client-approve` → **200 + 400** (app-level dedup). **Zero unhandled 500s** in the server log across the whole session. |
| §3 aggregate lost-update | `UsedQty` now computed via `CountCompletedUnitsAsync` (SQL COUNT), not read-modify-write. |
| §4 transactions | `CustomersController.Create/Update` and `JobRequestsController` assign/progress wrapped in `TransactionHelper.ExecuteInTransactionAsync`. |
| §5 intake-reject signatures | intake-reject → re-edit → resubmit now returns **`PendingClientMoiApproval`** (re-signing required). Fixed. |
| §6 pagination + retry | `page/pageSize` on customers/completedservices/jobrequests; `EnableRetryOnFailure()` on Npgsql. |

---

## 2. Completeness scorecard (100% = live-ready, no caveats)

Every feature below is **functional and in production**. The percentage reflects how much is *done vs. carrying a real gap* — a sub-100% score means "works, but has a documented gap that could bite," not "broken."

| Feature area | % | Evidence / gap |
|---|---|---|
| **Authentication & sessions** | 95% | Login, JWT, change-password, must-change-password gate, forgot/reset OTP (enumeration-safe, 429 cooldown, rate-limited) — all verified. **Gap**: 7-day tokens with no revocation — a deleted/demoted user keeps access until expiry (§3.2). |
| **User management** | 100% | CRUD, role validation, client-admin scoping, privilege-flag stripping, escalation blocks — all verified across reviews. |
| **Customer management** | 95% | CRUD, packages, holders, billing parties; create/update now transactional. **Gap**: update still deletes+recreates all `AccountHolders`, churning ids (survives via email re-link, but id-unstable) (§3.4). |
| **Products / catalog** | 100% | Seeded, CRUD, Figma catalog sync. |
| **Job requests & units** | 100% | Package→job sync, unit generation, assignment, aggregate via SQL COUNT. |
| **MOI workflow** | 100% | Issue, submit, dual client sign-off *with signatures*, intake approve/reject, recommend, approve, admin-override, rejection→re-edit→resign loop. Fully re-driven this round. |
| **MOA workflow** | 100% | Pack + checklist validation + optimistic concurrency, submit-review, Sharon approve, release, dual client sign-off, execution complete. |
| **AdminBypass (D1)** | 100% | Choice + note validation, admin-only completion, visible in Sharon's list, blocks client close. |
| **Documents** | 95% | Upload (server-side type allowlist enforced — verified `.txt` rejected, `.pdf` accepted), download round-trip byte-matches, folder + unit visibility scoping. **Gap**: single local volume, no redundancy/object storage — a scale/DR consideration for "a lot a lot of files" (§3.3). |
| **Notifications** | 98% | In-app + email (Resend) for workflow events + admin_bypass; bell/read endpoints work. **Minor**: stale bypass notification survives a mode reversal (§3.5). |
| **Invoicing** | 70% | Create (concurrency-safe numbering), list, per-customer filter all work. **Gap**: the "PDF" endpoint returns **`text/plain`**, not a real PDF (§3.1) — the one materially incomplete feature. |
| **Dashboard / stats** | 100% | Full admin metrics payload. |
| **Client portal** | 100% | my-company, scoped summary, MOI-approval-mode toggle. |
| **Package scheduling / tracking** | 100% | Schedule items sync from units; calendar data serves. (ICS feed is an unbuilt *enhancement*, not a gap.) |
| **Completed services / history** | 100% | Recording, JobRequestId-scoped filtering, pagination. |
| **Signatory management** | 100% | Provisioning, multi-company access, dedup overlaps/link (`/api/signatory-dedup`). |
| **Workflow configuration** | 100% | Templates seeded + admin config. |
| **Cross-tenant isolation & authorization** | 100% | Re-verified: every role sees only its tenant; no IDOR across companies. |
| **Concurrency & data integrity (Postgres)** | 95% | 409 mapping, COUNT aggregate, uuid stamps, 4 transactional actions — no 500s observed. **Gap**: concurrency is verified only *manually* — the 79 tests run on SQLite and cannot catch a Postgres race regression (§3.6). |

**Overall: the core product — the entire MOI/MOA workflow, multi-tenant access, documents, scheduling, client portal — is 100% and live-ready.** The weighted-down areas are peripheral (invoicing polish) or hardening (token revocation, storage DR, concurrency test coverage). **Nothing is broken.**

---

## 3. The final review — everything that is not 100% (this is the last pass)

Ordered by how much it should stop you. None of these are launch-blockers for the workflow; #1 is the only *feature* gap, the rest are hardening.

### 3.1 Invoicing — the PDF is a text stub (the one real feature gap) — 70%
- **What**: `GET /api/invoices/{id}/pdf` returns `Content-Type: text/plain` with a few plaintext lines (`InvoicesController.DownloadPdf`), not a PDF. Numbering, creation (concurrency-safe), and listing are complete.
- **Fix**: add QuestPDF (free for a company this size) and render a branded invoice; if you need line items, add an `InvoiceLine` child table (description, qty, unitPrice). ~1 focused change; the data model is already there.
- **Decision needed**: is invoicing actually in-scope for launch, or is billing done outside this system? If outside, drop the endpoint and mark the area N/A instead of 70%.

### 3.2 Auth — no token revocation (7-day window) — 95%
- **What**: JWTs live 7 days with no refresh/revocation. Deleting or demoting a user (`UsersController.Delete`, role change) does not invalidate their existing token — they retain their old access until it expires.
- **Fix**: either drop the access-token lifetime to ~24h (one-line, low-effort mitigation) or build refresh tokens + a `RefreshToken`/revocation table and check-on-request (full fix). For a small internal team the 24h reduction is likely enough.
- **Decision needed**: acceptable risk for your user base, or build revocation?

### 3.3 Documents — single local volume, no redundancy — 95%
- **What**: files live on one disk under `LGB_UPLOAD_ROOT=/data/uploads`. Correct and durable across redeploys, but no redundancy, no horizontal scale, and backups are manual (they're outside the DB, so a DB backup does *not* include them).
- **Fix**: extract the `IDocumentStorage` interface and add an S3/R2 implementation (the local impl stays for dev). Also: ensure `/data/uploads` is in your backup routine **separately** from the Postgres dump.
- **Decision needed**: at your file volume, is DR/backup of uploads handled? If not, this is more urgent than its 95% implies.

### 3.4 Customers — account-holder delete+recreate churns ids — 95%
- **What**: `CustomersController.UpdateCustomer` removes all `AccountHolders` and re-inserts them each save. It survives (signatory provisioning re-links by email), but holder ids change on every edit, which is wasteful and fragile for anything referencing them.
- **Fix**: diff-by-id upsert instead of delete+recreate.

### 3.5 Notifications — stale bypass alert after mode reversal — 98%
- **What**: if an admin flips an AdminBypass unit back to MoiMoa, the already-sent `admin_bypass` notification isn't cleared, leaving a dangling alert.
- **Fix**: in `ChooseWorkflow`'s MoiMoa branch, mark unread `admin_bypass` notifications for that job read.

### 3.6 Concurrency has no automated test coverage — 95%
- **What**: the Review #4 concurrency fixes (409 mapping, COUNT aggregate) are verified only by hand. The 79 unit tests run on SQLite, which serializes writes, so a future regression that reintroduces a Postgres race would ship green.
- **Fix**: add an integration test that (a) asserts the exception handler maps `DbUpdateConcurrencyException`→409 and unique-violation→409 (pure unit test, no DB needed — the mapping is already extracted for testing per the code comment), and (b) optionally a Testcontainers-Postgres test firing two parallel `client-approve`s. (a) alone closes most of the risk cheaply.

### Carry-overs still open (low priority, documented in earlier reviews, not launch-blocking)
- Cross-tenant form actions check state before authorization on some paths → 400 (phase oracle) instead of 404 (Review #4 leftover / R#2 N4). Low: no data leaks.
- Job-list visibility filtering still partly in-memory after the DB filters — pagination caps the blast radius, but the per-user visibility pass runs in C#. Fine at current scale.

---

## 4. Verified rock-solid — do NOT touch (regression-prone if "improved")
The full MOI/MOA state machine and its rejection loops; multi-signer sign-off (AllRequired/AnyOne); signature enforcement (D3); multi-session partial release; AdminBypass; cross-tenant isolation; user-management privilege controls; OTP enumeration-safety + cooldown; invoice number generation under concurrency; the Postgres schema (32 FKs, timestamptz, uuid stamps), UTC DateTime handling, and the optimistic-concurrency happy path. These have been driven live across five reviews and are stable.

---

## 5. Recommendation
You can stay live as-is — the workflow is complete and sound. To reach "100% across the board," the only *feature* work is **3.1 (invoice PDF)**; everything else in §3 is hardening you can schedule deliberately. If you do one more thing before considering this closed, make it **3.6** (the concurrency mapping test) so the Postgres fixes can't silently regress — it's cheap and it's the only thing standing between "verified by hand today" and "protected going forward."
