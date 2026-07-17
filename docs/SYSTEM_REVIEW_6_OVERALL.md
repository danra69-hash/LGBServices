# LGBServices — Overall System Review (Functionality + User POV)

Date: 2026-07-16. Method: full live simulation against **real PostgreSQL 16** (isolated DB, production data untouched), walking every role through its actual journey via the HTTP API — Admin (Sharon), internal prep staff (Nita), ClientAdmin, and two ClientSignatories (Alice + Bob) — plus a second tenant for isolation. This is a holistic review: what the system *does*, how it *feels* to each user, and where it's rough. It supersedes nothing — it consolidates six rounds of review into a current-state picture and surfaces the new findings from this pass.

---

## 1. Verdict

**The product is sound and live-ready. The core — the MOI/MOA compliance workflow — is genuinely well-built:** a coherent multi-role state machine, correct multi-tenant isolation, signature-backed sign-off, and (after the Postgres migration and Reviews #3–#5) correct behavior under real concurrency. Across a full lifecycle the user-facing status arc reads cleanly.

The gaps are now **governance/UX polish, not correctness**: one real workflow-bypass hole (§4.1), a couple of misleading numbers in the client dashboard (§4.2–4.3), and the previously-documented feature/hardening items (invoice PDF, token revocation). Nothing is broken; nothing loses data. The system does what it says.

---

## 2. Architecture snapshot

- **Backend**: ASP.NET Core 8, 188 C# files. Controllers stay thin; domain logic lives in static services (`JobHandoffService`, `TaskFormVisibilityHelper`, `ClientApprovalService`, …). Authorization is imperative via `AuthHelper`. Global RFC7807 error handling with concurrency/unique-violation → 409 mapping.
- **Data**: PostgreSQL (migrated from SQLite), EF Core migrations via a provider-filtered assembly (SQLite still works for dev/tests). 27 tables, 32 FKs, `timestamptz` throughout with a UTC value-converter, `uuid` optimistic-concurrency stamps on forms.
- **Frontend**: React 18 + Vite, ~106 files, still a large `App.tsx` shell (no router). API layer in `src/lib/api.ts`; JWT in `localStorage`.
- **Roles**: `Admin` (Sharon — intake, MOI/MOA sign-off, oversight), `User` (internal prep staff), `ClientAdmin` (client company admin), `ClientSignatory` (client signer). Internal-vs-client visibility is gated so staff never see client-only drafts.
- **Tests**: 79 xUnit (workflow helpers + the concurrency-mapping unit). CI runs tests + a Python workflow backtest.

---

## 3. Functional walkthrough — by role POV

### 3.1 Admin (Sharon) — the operational hub
Creating a customer with a **Professional Package** auto-generated **11 correctly-quantified job lines** (Prepare Resolution ×10, Follow-up ×10, plus the annual-compliance singles) and provisioned the client's ClientAdmin + signatory logins automatically. From here Sharon:
- Sees a job **only once the client releases it** (intake queue) — verified: job appeared in her list exactly at the "With LGB for review" step, not before.
- Approves/rejects intake, assigns prep staff, approves MOA, releases to client, and closes execution. Every transition worked and is authorization-gated.
- Dashboard returns real metrics (active customers, revenue, outstanding, completed) and a workflow notification feed (`moi_submitted`, `job_completed`).
**POV**: coherent and complete. One labeling nit — `totalRevenue` shows the *prorated remaining* package value (RM 5,556 of a RM 12,000 package), not booked revenue (§4.3).

### 3.2 Internal prep staff (Nita) — scoped to assigned work
- Never sees a job in the global list until it's released, and even then her work surfaces through **`my-tracker`**, not the job list — verified she picked up job 7 in her tracker (service, "Resolution prep", linked MOA) only after assignment.
- Fills the MOA pack (checklist validated), submits for review. Cannot approve intake or MOA (403) — correct.
**POV**: clean, focused workspace. The tracker-as-workspace model is a good design.

### 3.3 ClientAdmin — company cockpit
- `my-company` shows the profile, package, signer roles, and MOI-approval mode. `summary` is the landing dashboard.
- Sees all 11 company jobs with human-readable **display statuses** ("MOI not received", "Pending sign-off", "With LGB for review", "Resolution prep", "Ready for MOA", "MOA circulation", "Executing", "Completed").
- Chooses the workflow per task (MoiMoa vs AdminBypass), assigns, and tracks.
**POV**: mostly excellent — the display-status vocabulary is the strongest UX asset in the app. Two issues: the summary's **`teamMembers` count is wrong** (§4.2), and an Unset-mode job can be closed with no workflow (§4.1).

### 3.4 ClientSignatory (Alice / Bob) — the signers
- See the same company jobs; can issue an MOI, sign with a captured signature (required — enforced), and sign the MOA.
- Multi-signer honored: under `AllRequired`, the task stayed "Pending sign-off" until *both* Alice and Bob signed, then advanced. Double-signing is blocked ("already approved").
- Cannot perform admin actions (403).
**POV**: correct and legible.

### 3.5 The lifecycle, as the client actually sees it
A single job driven end-to-end produced this status arc, all correct:
`MOI not received → Pending sign-off → With LGB for review → Resolution prep → Ready for MOA → MOA circulation → Executing → Completed`.
This is the system's core value and it works. The **Print Pack** feature (new) renders the whole task — MOI + MOA + checklist + sign-off trail with embedded signatures + document index — as one printable HTML page, correctly access-scoped.

---

## 4. Findings from this review (new / ranked)

### 4.1 HIGH (governance) — Unset-mode jobs bypass the entire workflow
- **Reproduced**: as ClientAdmin, `POST /clientjobs/{id}/progress {markUnitComplete}` on jobs that had **no workflow choice made** → **HTTP 200, unit Completed**, for a single-qty job (job 6), a multi-qty session (job 10 unit 1), with **no MOI, no MOA, no sign-off**.
- **Root cause**: the D1 completion guard (`ClientJobsController` progress) only blocks when `JobWorkflowModes.IsMoiMoa(mode)` is true. Every job starts with `WorkflowMode = ""` (Unset). In the Unset state neither the MoiMoa branch nor the AdminBypass branch fires, so completion falls straight through.
- **Why it matters**: Unset is the **default for every job**. A client can skip all MOI/MOA governance simply by not choosing a workflow and marking the task done. D1 was meant to force the choice; it only enforced it for clients who explicitly picked MoiMoa.
- **Fix**: treat Unset as "choice required." In the progress handler, if `mode` is Unset (and the task type is one that should be governed), block completion with a message like "Choose MOI/MOA or send a request to LGB before completing." Alternatively, default Unset to behave like MoiMoa (blocked). Decide which service types, if any, are legitimately client-closable without a workflow and allowlist those.
- **Accept when**: a client cannot mark an Unset-mode governed job complete; picking MoiMoa or AdminBypass first is required.

### 4.2 MEDIUM (UX correctness) — Client summary `teamMembers` is always understated
- **Reproduced**: a company with 1 ClientAdmin + 2 signatories shows `teamMembers: 0`.
- **Root cause**: `ClientPortalController` counts only `Role == ClientAdmin` (excluding self). Signatories — the actual team — aren't counted.
- **Fix**: count all users for the customer except self (or all ClientAdmin + ClientSignatory). One-line predicate change.

### 4.3 LOW (labeling) — "Revenue"/"value" fields show prorated remaining, not booked
- `dashboard/stats.totalRevenue` and `clientportal/summary.activePackageValue` both surface the **prorated remaining** value (RM 5,556 from a RM 12,000 package bought mid-year), labeled as revenue/value. Accurate as "remaining active value," misleading as "revenue."
- **Fix**: relabel to "Active package value (remaining)" in the UI, or expose both booked and remaining. No logic change needed.

### 4.4 LOW (wording) — "MOI not received" persists after the client drafts an MOI
- After a signatory issues an MOI (a Draft exists) but before submitting, the client still sees `display = "MOI not received"` while `status = In Progress`. It means "not received by LGB," but reads as "you haven't started," which contradicts the fact they just created it.
- **Fix**: add a distinct "MOI drafted — not yet submitted" display state for the Draft-before-submit window.

### 4.5 LOW (data smell) — raw `status` regresses to "Pending" after client completes sign-off
- When the client finishes MOI sign-off and it lands at LGB, the raw `JobRequest.Status` shows "Pending" again (handoff `ClientSubmitted`). The **display status is correct** ("With LGB for review"), so users are unaffected, but any report or integration reading the raw `status` field sees a misleading value. Consider deriving a single source of truth (display status) or keeping `status` monotonic.

---

## 5. Carried-over open items (from Reviews #1–#5, still valid)

- **Invoicing (feature, ~70%)**: `GET /api/invoices/{id}/pdf` returns `text/plain`, not a PDF. Creation/numbering/listing work.
- **Auth token revocation**: 7-day JWTs, no revocation on delete/demote. Mitigate by shortening to ~24h or add refresh tokens.
- **Document storage DR**: single local volume; uploads are **not** in the DB backup — back up `/data/uploads` separately; consider S3/R2 at scale.
- **Frontend decomposition**: `App.tsx` is still a large no-router shell — a maintainability (not functionality) debt.
- **Cross-tenant form actions** return 400 (phase) before 403/404 on some paths — a minor existence oracle, no data leak.
- **Account-holder churn**: customer update still delete+recreates holders (id-unstable).

---

## 6. Rock-solid — verified again, do not disturb
The full MOI/MOA state machine and rejection loops; multi-signer sign-off with mandatory signatures; internal-vs-client visibility gating (staff never see client-only drafts; work surfaces via the tracker); AdminBypass (D1) including admin-only completion and Sharon-visibility; cross-tenant isolation (403 across companies); privilege-escalation blocks in user management; OTP enumeration-safety + cooldown; Postgres concurrency (409 not 500), UTC handling, and `UsedQty` via SQL COUNT; the new Print-Pack feature. 79 tests green.

---

## 7. Recommended priority
1. **§4.1** — close the Unset-mode bypass. It's the only finding that undermines the workflow's purpose, and it's a small, contained change. Do this before anything else.
2. **§4.2** — fix the team-member count (trivial, visible to every client).
3. **§4.3 / §4.4** — labeling/wording clarity (no logic risk).
4. Then the carry-overs by business priority: invoice PDF if billing is in-scope, token lifetime for security, uploads backup for DR.

The system is in good shape. After §4.1, the workflow governance is airtight; the rest is refinement you can schedule without launch pressure.
