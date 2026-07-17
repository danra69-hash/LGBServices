# System Review #7 — What the User Actually Sees (UX + Functionality)

Date: 2026-07-17. Method: booted against a **copy of the live 169-company seeded database** (the new `Pg_ClientActivatedAt` migration applied cleanly on real data), and drove each role while capturing the exact API payloads that render on their screens — customer list, package workboard, client portal tiles, progress bars, work tracker, dashboard. Focus this round: **the number/label/empty-state a real user reads**, not just whether the endpoint works.

Bottom line: the **0/0 package-workboard bug is fixed** and the new on-demand session model works. But a fresh-seeded system still *reads* as broken to a real user because of three headline-number/empty-state problems (§2). None are crashes — every screen loads, zero server errors — they're "the screen tells me the wrong story" issues.

---

## 1. Fixed / working — what the user now sees correctly

- **Admin package workboard (was 0/0 → FIXED).** Opening Adil Cita's package now shows all **11 service lines** with real progress — `Annual Return 0/1`, `Follow up with Reso signatory 0/10`, `Local Support Service 0/4`, each with a display status. Commit `853edd6` made `GET /api/jobrequests?customerPackageId=…` skip the internal-release filter for admins, so the full deliverables catalog is visible. This was the core frustration from last session and it's genuinely resolved.
- **New "start session on demand" model works.** A qty-10 line shows **10 dormant sessions**; the client clicks Add → `activate-session` marks unit 1 active (`ClientActivatedAt` set) → 1 active / 9 dormant, active session reads "MOI not received" (ready to start). Clean.
- **Customer list** renders all 169 companies with package/value/status.
- **Stability**: across the whole multi-role simulation, **0 unhandled exceptions / 500s**. Login, portal, workboard, dashboard all load.

---

## 2. What a real user sees that's wrong or confusing (ranked)

### 2.1 HIGH (visible everywhere) — Dashboard says "1,079 outstanding services" but the admin's job queue is empty
- **What the admin sees**: the dashboard tile **Outstanding Services = 1079**, but opening the main jobs list shows **0 jobs**. Two headline surfaces, opposite stories.
- **Why**: `DashboardController` counts *every* JobRequest with status Pending/In Progress (`outstandingServices = Count(Status==Pending||In Progress)`) — which is all 1,079 seeded lines, none of which a client has released yet. Meanwhile the job list applies the internal-release filter and shows only released work (0). So the tile counts the full catalog; the list counts active work.
- **Why it matters**: the admin reads "1,079 things to do," clicks in, sees nothing, and concludes the app is broken (exactly the reaction that started this thread).
- **Fix**: make the tile mean what it says. Either count only *released/active* work (jobs past the client-release gate) so it matches the queue, or relabel it "Package deliverables (total)" and add a separate "Active work" tile for the released count. Pick one; don't let the same screen show 1,079 and 0 for the same concept.

### 2.2 MEDIUM (every client company) — Team Members shows 0 despite a full signer list
- **What the client sees**: portal dashboard tile **Team Members = 0**. Adil Cita actually has **12 ClientSignatories + 1 ClientAdmin**.
- **Why**: `ClientPortalController` counts only `Role == ClientAdmin` (excluding self) → 0 for any company with one admin. Signatories — the actual team — aren't counted. (Flagged in Review #6 §4.2; still unfixed.)
- **Fix**: count all users for the customer except self (ClientAdmin + ClientSignatory). One-line predicate change. High visibility, trivial fix.

### 2.3 MEDIUM (the 56 "Add-ons only" companies) — blank screens on both client and admin side
- **What the client sees**: their company + package load fine, but the services area is **empty** — `openJobs: 0`, category progress empty, **0 service lines**, no button to do anything. The "0/0, can't key in" the user has been hitting.
- **What the admin sees**: the package workboard for these companies is **also blank** (0 lines).
- **Why**: these are non-cosec "Add-ons only" packages whose CubeV source rows have no itemised services (`addOns:[]`, `resoQty:0`) — verified against the original spreadsheet last session. There is genuinely nothing to render.
- **Fix (product decision, not a code bug)**: decide what these companies are.
  - If **ad-hoc/on-demand clients**: give the empty portal an explicit empty-state with a **"Request a service" action** (the ad-hoc `POST /clientjobs/issue-moi` path already exists on the backend) so the screen isn't a dead end. **Note the related bug**: ad-hoc requests are created as `TaskType="MOI"` and then don't appear in `my-jobs` (which filters to `Service`), so even after requesting, the client sees nothing — fix that filter or the ad-hoc task type.
  - If they **should have recurring work**: the itemisation must be supplied (it's not in CubeV); then they seed jobs like everyone else.

### 2.4 LOW — "Value"/"Revenue" shown on different bases in different places
- Client portal `activePackageValue` = **prorated remaining** (RM 3,699.95 of a RM 4,080 package); admin dashboard `totalRevenue` = **booked sum** (RM 480,936.93 across all packages). Same underlying data, two bases, both labelled generically. A user comparing the two screens can't reconcile them.
- **Fix**: label explicitly — "Active value (remaining)" on the client, "Booked package revenue" on the admin — or standardise the basis.

### 2.5 LOW — After a fresh seed, every "active work" screen is empty while catalogs are full
- 1,079 jobs all Pending, 778 pre-seeded **Draft** MOI shells, nothing released → the admin's job queue and the internal staff tracker (Nita) both show **0 items**, even though the catalog/workboards are full.
- This is *correct* behavior (work appears once clients start issuing MOIs), but with no empty-state copy it reads as "nothing works." A one-line empty state ("No active work yet — items appear here once clients start their sessions") would prevent the misread.

---

## 3. New feature audit — on-demand multi-qty sessions
Works functionally (dormant → activate → ready). One UX note: a qty-10 line defaults to showing 10 dormant sessions all reading "MOI not received," with no cue that the client must **Add/activate** a session before acting. Consider labelling dormant sessions distinctly (e.g., "Session 3 of 10 — not started") and making the primary action obviously "Start next session," so the client isn't unsure why 10 identical rows show "MOI not received."

---

## 4. Priority
1. **§2.1** — reconcile the "1,079 outstanding vs empty queue" contradiction. It's the single thing that makes the admin think the whole app is empty/broken, and it's the reproduction of this whole thread's original complaint.
2. **§2.2** — team-member count (trivial, every client sees it).
3. **§2.3** — decide the 56 add-ons-only companies' fate and fix the ad-hoc `my-jobs` filter so the empty portal isn't a dead end.
4. **§2.4 / §2.5** — labels and empty-state copy.

Everything in §1 is verified working on real data — don't regress the package-workboard fix or the session-activation flow. No crashes anywhere; these are all "the screen tells the wrong story," which for a live product the client logs into daily matters as much as correctness.

---

## 5. Shipped (2026-07-17)

Product decision for §2.3: treat Add-ons-only companies as **ad-hoc / on-demand** (empty catalog is correct; portal must offer a clear request path).

| # | Fix | Notes |
|---|-----|--------|
| §2.1 | `DashboardController.GetStats` | `outstandingServices` now counts only jobs past `InternalWorkVisibilityHelper.IsJobLineReleasedToInternal` (same gate as the admin queue). Tile relabelled **Active work**. Fresh seed → **0**, matching the empty queue. |
| §2.2 | `ClientPortalController.GetSummary` | Team count = ClientAdmin **+** ClientSignatory except self. Packages page shows a **Team members** tile. |
| §2.3 | `ClientJobsController` my-jobs + portal UX | External `my-jobs` includes `TaskType` **Service \|\| MOI** (ad-hoc `issue-moi` was invisible). Empty company → **Request a service** CTA; form defaults to on-demand. MOI lines bucket under **On-demand**. Admin package workboard empty copy clarifies Add-ons-only. |
| §2.4 | Labels | Client: **Active value (remaining)**; admin: **Booked package revenue**. |
| §2.5 | Empty states | `JobRequestsTable` + `MyWorkTracker`: copy that work appears after clients start/release sessions. |
| §3 | Dormant session UX | Status label `Session N of total — not started`; primary action **Start next session**; category tile shows “sessions not started”. |

**Do not regress:** package-workboard admin visibility (`customerPackageId` skips release gate); session activation / dormancy (`ClientActivatedAt`).

**Tests**: `dotnet test LGBApp.Backend.Tests` → **85** green.

**Handoff for next reviewer:** re-verify on live (or seed copy) that (1) admin dashboard Active work ≈ job queue length, (2) Adil Cita Team members ≈ 12, (3) an Add-ons-only company can Request a service and see the MOI under On-demand after submit, (4) multi-qty dormant labels / Start next session still match activation behaviour.

---

## 6. Deployment status + open work (verified 2026-07-17, post-§5)

### 6.1 §5 is live — no further deployment needed
Verified by inspection of the repo, not assumed:
- HEAD = `beb876f` "Fix Review #7 UX…", committed 2026-07-17 11:20 +0800. This doc is committed in that same commit.
- `main` is level with **both** remotes — `origin/main` (danra69-hash/LGBServices) and `testing/main` (Ryannnism/LGBTesting): 0 ahead, 0 behind.
- Railway (`railway.toml`, Dockerfile builder) and Vercel (`vercel.json`) both deploy via **GitHub integration on push**. The push *was* the deploy.
- `Pg_ClientActivatedAt` applies automatically at boot (`Data/DatabaseBootstrap.cs:38` → `context.Database.Migrate()`). No manual migrate step.

**Conclusion: everything in §5 is shipped. Nothing further to deploy for Review #7.**

Two standing notes (neither blocks §5):
- **CI does not gate deploys.** `.github/workflows/ci.yml` runs tests only; Railway/Vercel deploy off the push independently. **A red test run still ships to production.** Worth fixing.
- `Dockerfile:15-16` pins `Database__Provider=Sqlite`. These are image *defaults*; Railway dashboard service variables override them, and `dd8539b` documents the live Postgres cutover. Confirm the Railway variables if you want certainty — not believed to be a live problem.

### 6.2 ⚠️ Review #6 §4.1 (Unset-mode workflow bypass) is STILL OPEN — and it is the highest-priority bug in the system
Review #6 ranked this its **#1 priority, to do "before anything else."** Review #7 did not address it and did not mention it. **Re-verified today: still unfixed.**

`ClientJobsController.cs:474-508` — on `markUnitComplete`, the mode is resolved at `:477`, then:
- `:478` blocks if `IsMoiMoa(mode)`
- `:492` blocks if `IsAdminBypass(mode)` and caller isn't admin
- **Unset (`WorkflowMode = ""`, the default for every new job) matches neither branch → falls through to `:508 unit.Status = "Completed"`.**

A client can close any governed job with no MOI, no MOA, and no sign-off, simply by never choosing a workflow. **This is not a UX/labeling issue — it defeats the compliance workflow that is the product's core value.** Fix before treating the MOI/MOA workflow as client-ready (see §7).

### 6.3 Uncommitted WIP in the tree: the Print Pack feature
`git status` is dirty with a **half-committed** feature. Current state is *safe* but the trap is live:

| File | State |
|---|---|
| `LGBApp.Frontend/src/lib/api.ts` (`openTaskPack`, `downloadTaskPack`) | **committed** in `d9668c5` — but nothing committed calls it (dead code in prod) |
| `LGBApp.Frontend/src/components/JobRequestDetailsModal.tsx` | **modified, uncommitted** — adds the "Print pack" button |
| `LGBApp.Backend/Controllers/JobPackController.cs` | **UNTRACKED** — serves `api/jobs/{jobId}/pack` |
| `LGBApp.Backend/Services/TaskPackExportService.cs` | **UNTRACKED** |

Routes match on both sides (`api/jobs/{jobId}/pack`), so the feature is coherent — it is simply not committed.

> **Note for the record:** Review #6 §3.5 and §6 describe Print Pack as a working, verified feature. It was verified in a *working tree*, not in a shipped build. **It has never been deployed.** Do not count it as live.

**⚠️ THE TRAP:** the two backend files are **untracked**. `git commit -a` stages only *tracked* files, so it would commit the button and silently omit the endpoint — shipping a **404 in production**. Commit all three together or none.

Also untracked (docs only, safe to commit any time): `docs/CUBEV_SEED_RUNBOOK.md`, `docs/SYSTEM_REVIEW_5_COMPLETENESS.md`, `docs/SYSTEM_REVIEW_6_OVERALL.md`.

---

## 7. Ship-readiness by component

**How to read these numbers.** They are a **reviewer's judgment call, not a measurement** — there is no coverage metric behind them. They mean: *"of the work needed before a paying client uses this daily without hitting a bug, how much is done?"* They are calibrated to the **intended usage path** (§7.2). Off-path readiness is lower and less tested. Evidence for each row is Reviews #5–#7 plus direct code inspection on 2026-07-17.

### 7.1 Component table

| Component | Ready | Blocking gap | Evidence |
|---|---|---|---|
| **Multi-tenant isolation** | **99%** | None known. 403 across companies; privilege-escalation blocked. | R#6 §6, re-verified |
| **Auth / login / OTP** | **90%** | 7-day JWT (`JwtTokenService.cs:53`), **no revocation on delete/demote** — a fired user keeps access up to 7 days. Shorten to ~24h. | R#6 §5 |
| **Admin dashboard** | **95%** | Fixed §2.1/§2.4. Tile now matches queue. | §5, `DashboardController.cs:75` |
| **Customer + package mgmt** | **95%** | 169 companies render; Add-ons-only decided as ad-hoc. Account-holder churn (delete+recreate, id-unstable) remains. | §5, R#6 §5 |
| **Client portal** | **90%** | §2.2/§2.3/§2.4 fixed. §4.4 open: no "MOI drafted — not yet submitted" state (verified absent today) — mild confusion, not a bug. | §5 |
| **Internal staff tracker** | **95%** | Works; empty state shipped. Tracker-as-workspace model is sound. | §5, R#6 §3.2 |
| **On-demand multi-qty sessions** | **90%** | Functional + labels shipped. Newest code, least field exposure — watch it. | §5, R#6 |
| **MOI/MOA workflow (the core)** | **75%** | **§6.2 Unset-mode bypass is open.** The state machine itself is excellent and verified end-to-end; governance is not enforceable until Unset is closed. **This number is 95% the day §6.2 lands.** | R#6 §4.1, `ClientJobsController.cs:474-508` |
| **Notifications** | **90%** | Feed verified (`moi_submitted`, `job_completed`). | R#6 §3.1 |
| **Print Pack** | **0% shipped** (~85% written) | Code complete and coherent; **not committed, never deployed** (§6.3). | `git status` |
| **Invoicing** | **70%** | `GET /api/invoices/{id}/pdf` returns **`text/plain` as a `.txt`** (`InvoicesController.cs:173`) — it is not a PDF. Creation/numbering/listing work. | R#6 §5, re-verified |
| **Document storage** | **75%** | Works, but uploads live on one local volume and are **not in the DB backup**. Back up `/data/uploads` separately or move to S3/R2. | R#6 §5 |
| **Frontend architecture** | *debt, not user-facing* | `App.tsx` = **1,897 lines**, no router. Maintainability only; does not affect shipping. | verified today |
| **Less-reviewed surfaces** | **unknown — low confidence** | Workflow/form templates, signatory dedup, division groups, billing parties: controllers exist, but no review has driven them end-to-end. **Do not report a % here; drive them first.** | absence of evidence |

### 7.2 The intended usage path (what these numbers assume)

The percentages above hold for this path, which is what Reviews #6 and #7 actually drove:

1. **Sharon (Admin)** creates a customer with a package → job lines auto-generate with correct quantities; ClientAdmin + signatory logins auto-provision.
2. **ClientAdmin** logs into the portal → sees company, package, team, and service lines with human-readable display statuses.
3. **Client starts a session** (multi-qty: Add → `activate-session` → dormant becomes active).
4. **ClientSignatory issues an MOI**, signs with a captured signature (mandatory); under `AllRequired`, *all* signers must sign.
5. **Work releases to LGB** → appears in Sharon's intake queue *only now* (not before) → she approves and assigns prep staff.
6. **Nita (internal staff)** picks the job up in **`my-tracker`** (not the job list), fills the MOA pack, submits for review.
7. **Sharon** approves MOA → MOA circulation → client signs → **Executing** → **Completed**.
8. **Add-ons-only companies (56)** skip 2–7: empty catalog is correct; they use **Request a service** → ad-hoc MOI → appears under **On-demand**.

Status arc the client reads, all verified correct:
`MOI not received → Pending sign-off → With LGB for review → Resolution prep → Ready for MOA → MOA circulation → Executing → Completed`

**Where the path breaks today:** at step 3–4, a client who **never picks a workflow** can jump straight to "mark complete" and close the job — skipping 4 through 7 entirely (§6.2). The golden path is sound; the *unguarded shortcut off it* is the hole.

### 7.3 Honest overall answer

- **Ship to a client today on the intended path, for MOI/MOA compliance work:** yes, with the caveat that governance is **advisory, not enforced**, until §6.2 lands. Every screen loads, zero 500s, no data loss, isolation is solid.
- **Ship as a system you can't supervise:** not yet. §6.2 is the one thing standing between "good" and "airtight," and it is a small, contained change.
- **Do not sell:** invoice PDF (it is a `.txt`), Print Pack (not deployed).
- **Order:** §6.2 → JWT lifetime → uploads backup → invoice PDF → Print Pack commit → the unknown surfaces in §7.1.

---

## 8. Instructions: commit the Print Pack feature (§6.3)

**Task:** commit three files as one unit and push. **Do not** write new code. **Do not** fix §6.2 here — that is a separate task.

**Why this needs care:** two of the three files are untracked, so `git commit -a` would ship the button without the endpoint → 404 in production. Every step below exists to prevent that.

Work in `/Users/ryannnism/LGBServices`. Run steps **in order** and **read each output before continuing**.

**Step 1 — confirm the starting state.**
```bash
git status --short
```
Expect exactly these (plus untracked `.claude/` and `docs/*.md`):
```
 M LGBApp.Frontend/src/components/JobRequestDetailsModal.tsx
?? LGBApp.Backend/Controllers/JobPackController.cs
?? LGBApp.Backend/Services/TaskPackExportService.cs
```
If any of the three is **missing**, **STOP** and report — someone changed things; these instructions no longer apply.

**Step 2 — stage exactly three files, by name. Never use `-a` or `.`**
```bash
git add LGBApp.Backend/Controllers/JobPackController.cs \
        LGBApp.Backend/Services/TaskPackExportService.cs \
        LGBApp.Frontend/src/components/JobRequestDetailsModal.tsx
```

**Step 3 — verify all three are staged. This is the critical check.**
```bash
git diff --cached --name-only
```
The output **must list all three paths**. If it lists fewer than three, **STOP** — do not commit; report what you saw.

**Step 4 — confirm the route still matches on both sides.**
```bash
grep -n "api/jobs" LGBApp.Frontend/src/lib/api.ts | grep pack
grep -n "Route" LGBApp.Backend/Controllers/JobPackController.cs
```
Frontend must call `/api/jobs/${jobId}/pack`; backend must declare `[Route("api/jobs/{jobId:int}/pack")]`. If they disagree, **STOP** and report.

**Step 5 — build and test. Do not skip; CI does not gate the deploy (§6.1), so this is the only gate.**
```bash
dotnet build LGBApp.Backend
dotnet test LGBApp.Backend.Tests
cd LGBApp.Frontend && npm run build && cd ..
```
All three must succeed. Baseline is **85 passing tests** (52 `[Fact]` + 33 `[InlineData]` rows) — this change adds none, so expect **85 green**. **If any command fails, STOP and report the error. Do not commit a failing build — pushing deploys straight to production.**

**Step 6 — commit.**
```bash
git commit -m "$(cat <<'EOF'
Add task pack export endpoint and Print pack action.

Ships the JobPackController endpoint and TaskPackExportService alongside
the Print pack button, so the already-committed openTaskPack API client
has a live endpoint to call.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
EOF
)"
```

**Step 7 — push.** ⚠️ **This deploys to production immediately** (Railway + Vercel auto-deploy on push, §6.1). Only proceed if Step 5 was fully green.
```bash
git push origin main && git push testing main
```

**Step 8 — verify the deploy.**
```bash
git status -sb   # expect: ## main...testing/main  (no ahead/behind)
```
Then in the live app: open any job's details modal → click **Print pack** → a printable page must open in a new tab. **If it 404s, the backend files did not ship — report immediately; do not attempt a fix without checking Step 3's output.**

**Rules for whoever runs this:**
- Never `git add -A`, `git add .`, or `git commit -a` — they interact badly with the untracked-file trap.
- Never `--force`, never `--no-verify`, never rewrite history.
- Do not commit `.claude/` (local tooling config).
- The three docs (`CUBEV_SEED_RUNBOOK.md`, `SYSTEM_REVIEW_5_COMPLETENESS.md`, `SYSTEM_REVIEW_6_OVERALL.md`) may be committed **separately**, docs-only, any time.
- If **anything** deviates from the expected output, **stop and report rather than improvise**. A wrong push here is a production incident, not a local mistake.

---

## 9. Client procedure conformance (the flowchart) — gap analysis + implementation spec

### 9.0 Authority, scope, and a warning

**The client's flowchart is the authoritative specification of the business procedure.** Where the built system and the flowchart disagree, **the flowchart wins** and the code changes. This section supersedes any contrary assumption in Reviews #1–#6.

> **Read this before starting.** Reviews #1–#6 concluded the system was "live-ready." That judgment meant *"the code correctly does what the code intends"* — **those reviews never tested against this flowchart.** Conformance to the client's actual procedure is a **different and largely untested axis**. Do not treat prior green verdicts as evidence of conformance.

**This section is not a single task and cannot be done in one pass.** It contains **two blocking gates (§9.2, §9.3) that must be cleared by a human before any code is written**, and seven workstreams (§9.4). W1 is an architectural change, not a feature. Work strictly in order. **If you are an agent executing this: read §9.7 (Rules of engagement) first.**

Clause IDs below map to the flowchart panels: **R** = requester/MOI intake (orange), **T** = MOI template checkboxes (orange, red text), **S** = Sharon/Poh Li triage (light green), **C** = Cosec A (green), **M** = MOA process (teal), **MS** = MOA stage order (teal, red text), **B** = billing (blue).

### 9.1 Conformance matrix (verified against code 2026-07-17)

| ID | Clause (flowchart) | Built? | Evidence |
|---|---|---|---|
| R1 | System presets which **Group** the company belongs to | ✅ **Yes** | `Customer.DivisionGroupCode` → `DivisionGroup`; `WorkflowService.ResolveMoaWorkflowTemplateCodeAsync:124` |
| R2 | System presets requester's **HOD** to approve the MOI | ⚠️ **Partial / different shape** | `MOI_RECOMMEND` template: Draft → Recommend (`DivisionRecommender`) → MoiApproval (`MoiApprovalHolder`). Group-level recommenders (`DivisionGroupRecommender`), **not per-requester HOD**. No `HOD` concept exists anywhere. |
| R3 | Reminder to HOD **every 24h, up to 2 times** | ❌ **No** | **No scheduler exists** (§9.4 W1) |
| R4 | Requester prompted **after 48h** if HOD hasn't approved | ❌ **No** | Same — no scheduler |
| R5 | Email states subject+status; **no login required except MOI approver** | ⚠️ **Partial** | Email real (`Services/Email/ResendEmailSender.cs`, `IEmailSender`), but **notification-only**. All approval requires login. |
| T1 | **Banking matter** → auto final approver Teh & Dato' Lim | ✅ **Yes** | MOI field `bankSignatoryMatter`; step `MsTeh` gated `ConditionType="BankSignatory"`; `Dlcm` step `Always` |
| T2 | **With LOA** → auto MOA approvers | ✅ **Yes** | `Customer.HasLoa` → `MOA_WITH_LOA` template |
| T2b | With LOA → **cosec may add approvers when MOA starts** | ❌ **No** | No add-approver path exists |
| T3 | **Mandatory "last point of approval"** field when LOA ticked | ❌ **No** | Absent. (`lastPoint` in `SignatureCapture.tsx:23` is canvas coords — false positive.) |
| T4 | **Without LOA** → final approver Dato' Lim | ✅ **Yes** | `MOA_NO_LOA` final step `Dlcm` |
| T5 | Preset which companies have LOA | ✅ **Yes** | `Customer.HasLoa`, `LoaHoldersJson`, `CreateCustomerModal.tsx` |
| S1 | Sharon **+ Poh Li** receive and assign | ⚠️ **Partial** | Admin intake/assign exists (R#6 §3.1). "Poh Li" as a second named triager is **not modeled**; both would share the `Admin` role. |
| S2 | Determine **type of secretarial service** from preset dropdown (for billing) | ⚠️ **Unverified** | Products/services exist; the *triage-time* dropdown driving billing is **not confirmed** — must drive it before claiming |
| S3 | Ticket **bounces back** to requester if incomplete | ✅ **Yes** | Rejection loops verified (R#6 §6) |
| C1 | Cosec A notified on assignment | ✅ **Yes** | `WorkflowNotificationService`, `WorkflowNotifier` |
| C2 | Cosec A uploads resolution + **2 standard checklists (PDF)** | ⚠️ **Partial** | `JobItemDocumentsController` supports uploads; the **"2 standard checklists" as a required, named, enforced pair** is not modeled |
| C3 | Cosec A **inserts additional MOA approvers** | ❌ **No** | Step templates are static/seeded. Same gap as T2b. |
| C4 | Non-MOI service: cosec marks done & delivered | ✅ **Yes** | AdminBypass path (R#6 §6) |
| M1 | Notify **all** legal+secretarial at **Stage 1**, for **LGB / Bellworth / SWM groups only** | ❌ **No** | No stage-1 broadcast; no group-scoped distribution list |
| M2 | Email per MOA stage; no login except approvers | ⚠️ **Partial** | Same as R5 |
| M3 | Reminder each approver **every 48h, up to 3 times** | ❌ **No** | No scheduler |
| M4 | All cosec prompted **after 144h** if stage not approved | ❌ **No** | No scheduler |
| M5 | Approver **comments on approval**; ticket **bounces back to all cosec** | ⚠️ **Partial** | `WorkflowStepInstance.Comments` exists; **bounce-to-all-cosec on comment** not modeled |
| M6 | **Sequential** MOA stages | ✅ **Yes (engine)** | `WorkflowInstance.CurrentStepOrder`, `WorkflowService.AdvanceWorkflowAsync:326` |
| MS1–MS7 | The **specific 7-stage order** | ❌ **Chain differs** | See §9.3 — **BLOCKING DECISION** |
| B1 | Preset annual packages (resolutions, meetings, fee) | ✅ **Yes** | Packages/products; 169 companies seeded |
| B2 | Preset which company to invoice | ✅ **Yes** | `BillingPartiesController` |
| B3 | Track billing items + **quota used** | ⚠️ **Partial** | `UsedQty` via SQL COUNT (R#6 §6). Not reconciled to a billing view. |
| B4 | Cosec enters **routine daily jobs** + completion + type dropdown | ⚠️ **Unverified** | Must drive before claiming |
| B5 | **Periodic (3-monthly) billing report** for Finance Head | ❌ **No** | `InvoicesController` has only `[HttpGet]:20` and `{id}/pdf:145` |
| B6 | Invoice output | ❌ **Broken** | `InvoicesController.cs:173` returns **`text/plain` as `.txt`** — not a PDF |

**Score: 11 of 26 clauses fully built.** The **decision engine** (routing, conditions, LOA/banking, sequencing) is genuinely good and largely conformant. The **unattended-operation layer** (timed reminders, escalation, no-login approval, broadcast, billing reporting) is **~0%** and is what the client's procedure runs on.

### 9.2 🛑 BLOCKING GATE 1 — the approval matrix data does not exist in this repo

The flowchart references **"refer approval matrix tab"** (×5), **"refer cosec email tab"**, **"Group Legal and Cosec email tab"**, and **"refer Source tab"** (×4). These are tabs in a **client spreadsheet that is not in this repository.** I searched; it is not here.

That spreadsheet is the sole source of truth for:
- Which **Group** each company belongs to (R1)
- **Every email address** in the procedure (R2, R5, C1, M1, M2)
- Each requester's **HOD** (R2)
- Which companies have **LOA** (T5)
- **Mandatory MOA approvers per Group** (MS5)
- The **secretarial services dropdown** (S2, B4)
- **Package presets**: no. of resolutions, meetings, annual fee (B1)

> **🛑 STOP. Do not invent, infer, or guess any of this data. Do not derive an HOD from job titles. Do not guess an email address. Do not assume a Group from a company name.** Wrong routing data in a compliance approval chain means a legal document is approved by the wrong person — the worst failure this system can produce, and one that will look like it worked.
>
> **Required action: a human must obtain the spreadsheet and commit it under `docs/source/`.** Until then, W2/W4/W5/W6 (below) are **blocked** and must not be started. W1 and W3 may proceed — they are structural and data-independent.

### 9.3 🛑 BLOCKING GATE 2 — the MOA chain conflicts and a human must rule

The flowchart's stage order and the seeded chain are **not the same chain**:

| # | Flowchart (authoritative) | Built (`WorkflowConfigSeeder.cs`, `MOA_NO_LOA`) |
|---|---|---|
| 1 | Head of group secretarial (**Sharon**) | `SeniorManagerCoSec` — "Senior Manager, Company Secretarial" |
| 2 | **MOI requester** | `ProjectInitiator` |
| 3 | **MOI approver** | `HeadOfFinanceCfo` *(condition: FinanceRelated)* |
| 4 | **Teh SW** (if banking matter) | `CeoCooGm` *(condition: Applicable)* |
| 5 | **Mandatory MOA approvers preset per Group** | `MsTeh` *(condition: BankSignatory)* |
| 6 | **Additional approvers** added by Cosec A | `BoardMembers` *(condition: BoardApproval)* |
| 7 | **Dato' Lim** OR the MOI's last point of approval | `Dlcm` *(Always)* |

Both chains are plausible and they **overlap but do not align**. `MsTeh`≈"Teh SW" and `Dlcm`≈"Dato' Lim" survive in both, but their **positions differ**, and the built chain contains roles (CEO/COO/GM, Full Board) the flowchart does not, while the flowchart contains stages (MOI requester, MOI approver, cosec add-ons) the built chain does not.

**The built chain came from somewhere** — likely an earlier spec. Silently overwriting it may destroy a deliberate decision.

> **🛑 STOP. A human must confirm, in writing, which chain is authoritative — and whether `MOA_WITH_LOA` and `MOA_SWM` follow the same replacement.** Do not reconcile these yourself. Do not "merge" them. Ask.
>
> **Also required from a human:** is **`DLCM` = Dato' Lim?** and is **`MsTeh` = Teh SW?** Everything in W5 depends on these two identity claims and **neither is proven by the code** — they are my inference from name similarity.

### 9.4 Workstreams

Ordered by dependency. **W1 first — several others are impossible without it.**

---

#### W1 — Scheduler foundation (⚠️ architectural; blocks R3, R4, M3, M4)

**The problem:** the application has **no ability to act because time passed.** `Program.cs` registers **no `AddHostedService` and no `BackgroundService`**. `WorkflowNotifier.cs` contains "Reminder:" strings (`:74`, `:131`) but they are **event-driven** — sent when a user acts — and **no controller calls them** (verified: zero hits). Four clauses (R3, R4, M3, M4) are pure elapsed-time rules and are **structurally impossible today**.

**This is not a feature. Do not attempt it as a small change.**

**Design constraints — read carefully:**
1. `IEmailSender` and `WorkflowNotifier` are registered **`AddScoped`** (`Program.cs:72,76,78`). A `BackgroundService` is a **singleton** — it **must not** inject scoped services directly. Inject `IServiceScopeFactory`, call `CreateScope()` **inside each tick**, and resolve from that scope. Getting this wrong throws at startup or silently captures a dead `DbContext`.
2. **Reminders must be idempotent and capped** (R3: "up to 2 times"; M3: "up to 3 times"). A tick-based sender with no persisted state **will re-send on every tick and spam the client's executives.** You must persist per-target state: what was sent, when, and how many times. Add a table (e.g. `ReminderLog`: target entity + kind + sent count + last sent UTC) via an EF migration, **Postgres + the SQLite path** (`Data/SqliteSchemaMigrator.cs`) — this repo is dual-provider.
3. Railway may run **more than one instance**. Two instances ticking the same rule = double emails. Either guarantee single-instance or take a DB-level lock/claim before sending.
4. **All timestamps are `timestamptz` + UTC** via a value converter. Compute elapsed time in **UTC only**. The client's hours (24/48/144) are wall-clock elapsed, not business hours — **confirm with the client** whether weekends count. *Do not assume.*

**Acceptance:** a job sitting unapproved for 24h produces **exactly one** HOD reminder; at 48h a second and **no more ever**; the requester is prompted once at 48h. Prove it with tests that inject a clock — **do not `Thread.Sleep`**. Introduce an injectable time abstraction; `DateTime.UtcNow` is called directly throughout (e.g. `ClientJobsController.cs:509`) and is untestable as-is.

**Recommended:** implement the timer + `ReminderLog` + tests with **zero sends** first (log only), confirm the schedule fires correctly against real data, and only then connect `IEmailSender`. **An email bug here reaches the client's executives and cannot be recalled.**

---

#### W2 — HOD approval on MOI (R2) — 🛑 blocked by §9.2

Today: `MOI_RECOMMEND` routes Draft → `DivisionRecommender` (a **Group-level** recommender, `DivisionGroupRecommender`) → `MoiApprovalHolder`. The flowchart wants **the requester's own HOD**, presetting per requester from the approval matrix.

These are different data models: **group-level** vs **per-person**. Adding an `HodUserId` (or equivalent) to the user/requester record is likely required, plus a new `AssigneeType` (e.g. `"RequesterHod"`) resolved in `WorkflowService.ResolveAssigneeName:225` and authorized in `CanUserApproveStepAsync:284`.

**Do not start until the approval matrix is in the repo** — the mapping *is* the feature. **Ask a human** whether HOD replaces `DivisionRecommender` or sits alongside it.

---

#### W3 — Close the Unset-mode bypass (§6.2) — do this early; not blocked

Already documented in §6.2 and re-verified. In this section's light it is **more serious than Review #6 framed it**: the client's entire procedure is approval-gated, and this lets a requester close a job **with no approval whatsoever**. `ClientJobsController.cs:474-508` — Unset (`""`, the default for every job) matches neither the `IsMoiMoa` guard (`:478`) nor the `IsAdminBypass` guard (`:492`) and falls through to `:508 unit.Status = "Completed"`.

**Fix:** treat Unset as "choice required" and block completion. **Ask a human** which service types (if any) are legitimately client-closable without a workflow, and allowlist only those. **Acceptance:** a client cannot complete an Unset-mode governed job; choosing MoiMoa or AdminBypass first is required. Add a test.

---

#### W4 — No-login email approval (R5, M2) — 🛑 blocked by §9.2; ⚠️ security-sensitive

The flowchart says **"No system log-in required other than MOI approver"** — MOA approvers act **from email**. The built system requires login for every approval. This is the **largest philosophical gap** and the one most likely to sink adoption: the client is telling you their executives will not log in.

There is an existing pattern to study: `Services/PasswordResetService.cs` already issues single-use emailed tokens.

> ⚠️ **This creates an authentication bypass by design.** A link in an email that approves a legal document is a bearer credential. It must be: single-use, short-lived, scoped to one step of one instance, revoked on use, and **never** granting a session. Get a human to review the design **before** implementation. Do not reuse JWTs (7-day, unrevocable — `JwtTokenService.cs:53`).

**Ask a human:** does "no login" apply to MOA approvers only, or to the stage-1 broadcast recipients too?

---

#### W5 — MOA chain conformance (MS1–MS7, T2b, C3, M1, M5) — 🛑 blocked by §9.2 **and** §9.3

Once **both** gates clear:
- Re-seed the MOA chain to the confirmed order. **`WorkflowConfigSeeder.SeedWorkflowTemplates` early-returns if `context.WorkflowTemplates.Any()`** — it will **silently do nothing** on any existing database. Re-seeding live therefore requires a real migration plan, **and in-flight `WorkflowInstance` rows already copy their steps** (`InitializeMoaWorkflowAsync:165` snapshots templates into `WorkflowStepInstance`). **Decide with a human what happens to in-flight approvals — they will not pick up the new chain.** Do not migrate live data on your own judgment.
- **Cosec-added approvers (T2b, C3)** — needs an insert-step-at-runtime path into `WorkflowStepInstance`. The engine snapshots steps at init; inserting mid-flight must renumber `StepOrder` without breaking `CurrentStepOrder`. Non-trivial; write tests.
- **"Last point of approval" (T3)** — mandatory MOI field when `withLOA` is true; MS7 reads "Dato' Lim **OR** follow MOI's last point of approval record," so T3 **feeds** MS7. Build T3 before MS7.
- **Stage-1 broadcast (M1)** — restricted to **LGB / Bellworth / SWM groups only**. Group scoping exists (`DivisionGroupCode`); the distribution list does not.
- **Bounce-back on comment (M5)** — approver comments must return the ticket to **all** cosec team members.

---

#### W6 — Billing (B3, B4, B5, B6) — 🛑 B1/B4 blocked by §9.2

- **B6 (do first, smallest, real):** `InvoicesController.cs:173` returns `File(bytes, "text/plain", "{number}.txt")`. It must produce an actual PDF. No PDF library is currently referenced — **ask a human** which to add.
- **B5:** no periodic billing report exists. Needs a **3-monthly** report for the Finance Head — likely W1's scheduler (delivery) + a new endpoint (content).
- **B3:** `UsedQty` (SQL COUNT) exists but is not reconciled into a billing view showing quota used vs package.
- **B4/S2:** the routine-daily-job entry and the secretarial-services dropdown are **unverified** — drive them in the live app before writing code; they may partly exist.

---

#### W7 — Verify the unverified (S2, B4, C2) — not blocked; cheap; do it before W6

Three clauses are marked ⚠️ **only because no review has ever driven them.** Absence of evidence is **not** evidence of absence — **drive each in the running app and report what you find.** Do not write code for them until you know what exists. This may shrink W6.

### 9.5 Production check: is email actually being delivered?

`Program.cs:74-78`:
```
var resendKey = builder.Configuration["Email:ResendApiKey"];
if (!string.IsNullOrWhiteSpace(resendKey)) → ResendEmailSender
else                                       → LoggingEmailSender
```
**If `Email:ResendApiKey` is unset in Railway, every email in this procedure silently goes to the log and no one is notified** — with no error and no user-visible signal. The client's procedure is **email-driven end to end**.

**Verify this in the Railway dashboard before anything else in §9.** It is a 2-minute check that could invalidate assumptions across every workstream. Report the result; do not assume it is configured.

### 9.6 Revised readiness against *this* spec

§7's numbers were calibrated to the intended path **as the reviews drove it**, not to the client's procedure. Measured against the flowchart:

| Component | §7 said | vs. flowchart | Why |
|---|---|---|---|
| MOI/MOA decision engine | 75% | **~55–60%** | Routing/conditions/LOA/banking genuinely good; unattended-operation layer ~0% |
| Reminders & escalation (R3,R4,M3,M4) | *not assessed* | **0%** | No scheduler exists at all |
| No-login approval (R5,M2) | *not assessed* | **0%** | Login required for every approval |
| Billing | 70% | **~50%** | Quarterly report absent; invoice is a `.txt` |
| Multi-tenant isolation | 99% | **99%** | Orthogonal to the procedure; unaffected |

**Honest summary: the thinking is ~60% done; the automation is ~0%.** The engine is the hard part and it is largely right. What's missing is the layer that makes the procedure run **without anyone watching** — which is precisely what the client described.

### 9.7 Rules of engagement (read before executing any of §9)

1. **Order:** §9.5 check → W7 (verify) → W3 (bypass) → W1 (scheduler) → then gated work. **Never start a 🛑-blocked workstream.**
2. **Never invent routing data.** No guessed emails, HODs, Groups, LOA flags, or approver names. If it isn't in the committed spreadsheet, **stop and ask**. A confidently wrong approver is worse than a blocked task.
3. **One workstream per commit.** Never mix W-numbers.
4. **Every commit must pass** `dotnet build LGBApp.Backend`, `dotnet test LGBApp.Backend.Tests` (baseline **85 green**), and `npm run build` in `LGBApp.Frontend`. **CI does not gate deploys (§6.1) — a push to `main` deploys immediately.** Never push a red build.
5. **Never `git add -A` / `git add .` / `git commit -a`** — see the §6.3 untracked-file trap.
6. **Migrations are dual-provider.** Postgres (`Migrations/Postgres/`) **and** the SQLite path (`Data/SqliteSchemaMigrator.cs`). They apply at boot (`DatabaseBootstrap.cs:38`) — a bad migration breaks production startup.
7. **Emails are irreversible.** Log-only first, connect the sender last (W1).
8. **When the flowchart and the code disagree, the flowchart is right — but tell a human before you change the code.** Several disagreements (§9.3) look like deliberate past decisions.
9. **Report, don't improvise.** If reality doesn't match this document, **stop and say so.** This document was written 2026-07-17 against commit `beb876f`; if you're reading it later, verify before trusting it.
10. **Do not mark any clause ✅ without driving it end-to-end in a running app.** "The code looks right" is how Reviews #1–#6 concluded a never-deployed feature (Print Pack, §6.3) was working.

---

## 10. Final review — post-implementation (2026-07-17, HEAD `865e697`)

Method: reviewed all 8 commits after `beb876f`, traced each claim to running code, ran the full backend suite and the frontend build. Everything below is verified, not assumed.

### 10.0 Correction to §9.2 — I was wrong

**§9.2 claimed the source spreadsheet was not in the repo and made it a blocking gate. That was incorrect.**

`docs/source/COSEC_Billing_Tracking_2026_CubeV.xlsx` has been committed **since 2026-07-13 09:50** — four days before §9 was written. Its tabs are exactly the ones the flowchart cites:

`System Request` · `SOURCE` · `Approval Matrix` · `Cosec Email` · `Group Legal Email` · `Cosec Workdone Tracking` · `Annual Retainer Services`

I searched for the flowchart's tab *references* but never looked in `docs/source/`. **Blocking Gate 1 was never real** — the data was available the whole time, which is why W2/W4/W5/W6 could proceed. The §9.2 gate is void; disregard it. The *principle* stands (never invent routing data), but the data exists and is authoritative. §9.3's gate was correctly resolved by the client ruling the flowchart authoritative.

### 10.1 Verified working — real progress

| Item | Verdict | Evidence |
|---|---|---|
| **W3 — Unset bypass CLOSED** | ✅ Correct | `JobWorkflowModes.BlocksCompleteUntilWorkflowChosen`; guard at `ClientJobsController.cs:478-488` sits **before** the MoiMoa/AdminBypass branches, so Unset can no longer fall through. `UnsetWorkflowBypassTests.cs`. Blocks *all* Unset — the safe choice. |
| **MOA chain = flowchart MS1–MS7** | ✅ Exact | `BuildFlowchartSteps`: HeadOfGroupSecretarial → MoiRequester → MoiApprover → TehSW (BankSignatory) → GroupMandatory → CosecAdded → FinalApprover (Dato' Lim). Legacy `SeniorManagerCoSec`/`Dlcm` gone. `MoaFlowchartChainTests.cs` asserts all 3 templates. |
| **Reseed runs & is idempotent** | ✅ Wired | `SeedReferenceData:27` → runs on every boot via `Program.cs:226`; early-returns on `FlowchartHeadStepKey`. |
| **Approval Matrix seeded** | ✅ Real data | 36 rows / 34 requesters / 13 approvers, groups LGB·BELLWORTH·SWM — matches the flowchart's three groups. |
| **1:1 MOI approval (R2)** | ✅ Wired | `TryBindMatrixApproverAsync` binds `RequiredApproverEmail` at submit (`JobHandoffService.cs:72`); `FindMoiApprovalHolderForUser` returns `null` for non-matrix approvers. |
| **Seed JSONs ship in the image** | ✅ Verified | All 3 have `CopyToOutputDirectory=PreserveNewest` (`csproj:22-32`); Dockerfile copies `/app/publish`. **The silent-skip risk is closed.** |
| **No secrets committed** | ✅ Clean | `ResendApiKey` is `""` in both appsettings; key comes from env. |
| **Dual-provider migrations** | ✅ Correct | Postgres `Pg_MandatoryMoaApprovers`, `Pg_CubeVOps`; SQLite `EnsureColumn` for all new columns (`SqliteSchemaMigrator.cs:455-457`). |
| **Build + tests** | ✅ Green | `dotnet test` → **100 passed, 0 failed** (was 85). Backend + `npm run build` both clean. |
| **Deployed** | ✅ Live | Level with `origin/main` **and** `testing/main`; nothing unpushed. |

**Print Pack (§6.3) shipped correctly** (`aeb9b44`) — all three files committed together; the untracked-file trap was avoided.

### 10.2 🔴 HIGH — MS5 is not wired end-to-end: the Group mandatory approvers are dead data

**FIXED 2026-07-17** (`ResolveAssigneeName` for `GroupMandatoryApprovers`):

Priority is now **Start-MOA override → company `MoaApproversJson` → Group `DivisionGroup.MandatoryMoaApproversJson` (flowchart MS5)**. Bellworth/SWM group names (Kevin Kuok / Janice Lim / Ho De Leong / Shirley Nicholas) stamp onto the step when company list is empty. LGB group remains empty until Admin sets a company list or override.

<details><summary>Original finding (kept for audit)</summary>

**The most important finding of this review.** The flowchart's MS5 is *"Mandatory MOA approvers preset earlier follows Group."* The code presets them by Group — **and then never reads them.**

- `EnsureGroupMandatoryApprovers` writes real names into `DivisionGroup.MandatoryMoaApproversJson`: **BELLWORTH → Kevin Kuok; SWM → Janice Lim, Ho De Leong, Shirley Nicholas; LGB → (empty)**.
- Older `ResolveAssigneeName` read only form/customer JSON — never the DivisionGroup column.

</details>

### 10.3 🟠 MEDIUM — matrix binding fails open and silently

**Partially addressed 2026-07-17:** bind failures now **log** submitter email + company + form id; `requestedByName` is read from form JSON (was hard-coded `null`). Still **fails open** to company MOI approvers when no matrix row — intentional for non-matrix companies; do not block without a further human ruling.

### 10.4 🟠 MEDIUM — MS6 (Cosec-added approvers) is a placeholder that always skips

`StepApplies`: `"CosecAdded" => false` — the step **never applies**. This is honestly commented (*"inserted at runtime (C3) — skip until then"*), but it means flowchart clauses **T2b and C3 remain unimplemented**, and the seeded step 6 is inert. Conformance-wise MS6 is **not** delivered.

### 10.5 🔴 HIGH — the automation layer is still absent (unchanged)

Re-verified at HEAD: **no `AddHostedService`, no `BackgroundService`, no reminder-state model, no approval tokens.**

- **R3, R4, M3, M4 = 0%** — every timed reminder/escalation (24h×2, 48h prompt, 48h×3, 144h prompt). W1 not started.
- **R5, M2 (no-login approval) = 0%** — W4 not started.
- **B5 (quarterly billing report) = 0%**; **B6 invoice is still `text/plain`** (`InvoicesController.cs:173`).

The eight commits advanced the **decision engine**; they did not touch the **unattended-operation layer**. That remains the bulk of the remaining work and the part the client's procedure actually runs on.

### 10.6 🟡 Risks to confirm (not bugs — verify before client demo)

1. **Real client emails are now seeded.** `InternalStaffSeeder` contains live `@taliworks.com.my`, `@lgb.com.my`, and personal Gmail addresses. Staff seeding runs when `SEED_STAFF=true` (or Development). **If `SEED_STAFF=true` and `Email:ResendApiKey` are both set in Railway, testing will email real client executives.** `WorkflowNotifier` sends on submit/approve/remind. **Verify both env vars in the Railway dashboard before any live exercise.** Emails cannot be recalled.
2. **§9.5 still unanswered.** If `Email:ResendApiKey` is unset, `Program.cs:76-78` silently swaps in `LoggingEmailSender` — every notification goes to the log with no error. Still a 2-minute dashboard check; still not done.
3. **Cancel-in-flight is silent.** `EnsureMoaFlowchartChain` sets Active MOA instances to `"Canceled"` with only a `Console.WriteLine` — no notification, no resume path. Harmless now (nothing was in flight), but it re-arms on any future chain change.
4. **Seeded staff share one password** (`SEED_STAFF_PASSWORD`) with `MustChangePassword = true`. Acceptable, but real named executives now have accounts — confirm intended.

### 10.7 Revised conformance score

| Layer | Before (§9.6) | **Now** | Change |
|---|---|---|---|
| MOA chain **order** (MS1–MS7) | ❌ diverged | ✅ **exact** | Fixed (`614f209`) |
| MOA **approvers** (MS5) | ❌ | ✅ **Group fallback wired** | Override → company → Group |
| MS6 cosec-added (T2b/C3) | ❌ | ❌ **placeholder** | Inert by design |
| MOI 1:1 approval (R2) | ⚠️ partial | ✅ **wired** (fails open) | Real progress |
| Compliance gate (W3) | 🔴 open bypass | ✅ **closed** | Real progress |
| Reminders/escalation (R3,R4,M3,M4) | 0% | **0%** | Untouched |
| No-login approval (R5,M2) | 0% | **0%** | Untouched |
| Billing (B5,B6) | ~50% | **~50%** | Untouched |

**Clauses fully built: 11 → ~15 of 26.** The decision engine is now **substantially conformant**; MS5 is the one place where it *appears* conformant and isn't.

**Overall: the engine is ~70% done; the automation is still ~0%.**

### 10.8 Recommended next actions (ordered)

1. ~~Check Railway env: `Email:ResendApiKey` and `SEED_STAFF`.~~ **Done 2026-07-17:** Resend key set; `SEED_STAFF=false` (safe).
2. ~~Rule on MS5 (§10.2).~~ **Done:** flowchart Group is the default; Admin company/override still win when set.
3. ~~Log matrix bind failures (§10.3).~~ **Done** (still fail-open; block-vs-fallback ruling still open if desired).
4. **W1 scheduler** — the largest remaining piece; log-only first, sender last.
5. Then W4 (no-login), MS6/C3, B6 (invoice PDF), B5 (quarterly report).

**Do not regress:** the W3 Unset guard; the MS1–MS7 chain + its idempotent reseed; matrix binding at submit; Print Pack; the §5 UX fixes; MS5 Group fallback.

---

## 10. Progress since §9 (executed 2026-07-17)

### 10.1 §9.5 Email delivery — **FAILING in production**
Checked Railway service `LGBTesting` variables via CLI (`railway variables` | Email/Resend). **No `Email:ResendApiKey` / `Email__ResendApiKey` (or any Email_*) variable is set.**

Per `Program.cs:74-78`, production is therefore on **`LoggingEmailSender`** — every workflow email is silently written to logs only. **The client's email-driven procedure is not delivering.** Fix: set `Email__ResendApiKey` (and from/domain vars the Resend sender expects) on Railway, redeploy, then send a test notification.

### 10.2 §9.2 Spreadsheet gate — **CLEARED**
File is present and tracked: `docs/source/COSEC_Billing_Tracking_2026_CubeV.xlsx` (byte-identical to Downloads `COSEC Billing Tracking 2026 (coheads use only)(CubeV).xlsx`).

Tabs present (exactly what §9.2 required):
| Tab | Content |
|---|---|
| **SOURCE** | 285 companies: fee, reso qty, assisting, bill-to, **Group / Division** (LGB 88, SWM 15, Bellworth 6, Not Applicable 57, blank 45) |
| **Approval Matrix** | Per-group MOI requester → MOI approver (HOD), banking MOA (Teh SW), mandatory MOA, final (Dato' Lim), LOA companies |
| **Cosec Email** | Poh Li, Nita, Siti, Nadia |
| **Group Legal Email** | Datin Raj, Seet Mei, Dee Nee, Sutina |
| Cosec Workdone Tracking / Annual Retainer Services | Billing/ops reference |

**Confirmed from matrix (answers §9.3 identity questions in part):**
- **Teh SW** = `teh@taliworks.com.my` → maps to built `MsTeh`
- **Dato' Lim** = `lcm@lgbgroup.com` → maps to built `Dlcm`
- Banking MOA approver is Teh SW across LGB / Bellworth / SWM
- Final last point is Dato' Lim for LGB & Bellworth; SWM matrix leaves final blank / uses mandatory list differently
- LOA companies listed: Exitra, Exitra Solutions, LGB Properties (M), SWM Environments Holding, SWM Environment, SWM Solutions, Sumber SWM, Edaran SWM, SWM Enviro Holdings, SWM Enviro, SWM GC Energy (JV)

W2/W4/W5/W6 are **no longer blocked for missing data** — but **§9.3 chain-order ruling is still required before W5 code**.

### 10.3 §8 Print Pack — **SHIPPED** (`aeb9b44`)
Followed §8 steps exactly (three files only, route match verified, build+85 tests+FE build green, push both remotes). Live: job details → **Print pack**.

### 10.4 W3 Unset-mode bypass — **SHIPPED** (`40ac499`)
- `JobWorkflowModes.BlocksCompleteUntilWorkflowChosen` — Unset/blank blocks complete
- `ClientJobsController` mark-complete returns 400 until MoiMoa or AdminBypass chosen
- Client portal Done on Unset opens the workflow-choice modal instead of completing
- Tests: `UnsetWorkflowBypassTests` → suite **92** green (was 85)

**Allowlist decision still open (ask human):** are *any* service types legitimately client-closable with no workflow? Current ship: **none** — every mark-complete requires an explicit choice (matches flowchart C4: non-MOI still uses AdminBypass / note path).

### 10.5 Still blocked / next (human input required)

**Before W5 (MOA chain), answer in writing:**

1. Is the **flowchart** MS1–MS7 order authoritative over the seeded `MOA_NO_LOA` chain in `WorkflowConfigSeeder`? (Yes / No / Hybrid — describe)
2. Do `MOA_WITH_LOA` and `MOA_SWM` follow the **same** replacement?
3. Confirm **DLCM = Dato' Lim (`lcm@lgbgroup.com`)** and **MsTeh = Teh SW (`teh@taliworks.com.my`)** for production routing.
4. For in-flight `WorkflowInstance` rows when the chain changes: leave as-is / migrate / cancel?

**Immediate next (not blocked):** set Railway Resend key (§10.1) → W7 drive S2/B4/C2 in live app → W1 scheduler foundation (log-only first).

**Do not start:** W2/W4/W5 until §10.5 answers; W6 B6 PDF after PDF-library choice from human.

---

## 11. Human rulings + W5 shipped (2026-07-17)

### 11.1 Rulings (locked)

| # | Answer |
|---|---|
| 1 | Flowchart MS1–MS7 is authoritative over seeded chain |
| 2 | Same replacement for `MOA_NO_LOA`, `MOA_WITH_LOA`, `MOA_SWM` |
| 3 | `MsTeh` / Teh SW = `teh@taliworks.com.my`; `Dlcm` / Dato' Lim = `lcm@lgbgroup.com` |
| 4 | **B — cancel in-flight** Active MOA `WorkflowInstance` rows on upgrade (only Ryan using live) |
| 5 | Unset complete blocked; AdminBypass remains the one non-MOI path |

### 11.2 W5 chain reseed — shipped

- Templates rebuilt to MS1–MS7 (`HeadOfGroupSecretarial` … `FinalApprover` / Dato' Lim).
- `CosecAdded` condition skips MS6 until C3 lands.
- Active MOA workflows → `Canceled` once on upgrade; `GetWorkflowForMoaAsync` ignores Canceled so Start Workflow can re-init.
- `DivisionGroup.MandatoryMoaApproversJson` + CubeV names for Bellworth/SWM.
- Tests: `MoaFlowchartChainTests` — suite **97** green.

**Deferred (still W5):** T3 last-point field, C3 Cosec insert-step, M1 broadcast, M5 bounce-on-comment.

### 11.3 Live email-chain test accounts

After Resend is configured on Railway, seed staff with `SEED_STAFF=true` + `SEED_STAFF_PASSWORD=…`:

| Email | Role | Use |
|---|---|---|
| `ryannnism@gmail.com` | Admin (Sharon-class) | Intake / MOA approve |
| `ryannnism@berkeley.edu` | ClientAdmin | Client portal / MOI |

Both created/updated by `InternalStaffSeeder` when staff seed runs. Client links to the first Active customer.

---

## 12. CubeV ops conformance (2026-07-17)

### In scope shipped

| Workstream | What |
|---|---|
| B Staff | Real Cosec/Legal emails; Sharon + Poh Li Admin |
| A Matrix | `MoiApprovalMatrixEntries` + 1:1 MOI bind/sign; client MOI policy UI removed |
| E MOA Admin | `Customers.MoaApproversJson` + Start-MOA `MoaApproversOverrideJson` |
| C Package notify | `CompletionNotifiedAt` → notify ClientSignatories when all package jobs Completed |
| D Workdone | Idempotent import → `CompletedServices` (no new UI) |
| F SOURCE | Upsert companies **rows 2–167 only** (`CubeVCustomerSeeder`); addon menu placeholders skipped |

### Still out of scope

W4 no-login email approval · W1 reminder scheduler · C3 mid-flight Cosec insert

### Ops

- Migration `20260717070000_Pg_CubeVOps` (+ SqliteSchemaMigrator)
- Re-run `dotnet run -- seed-full` to upsert SOURCE 2–167 + workdone map
- One-shot `SEED_STAFF=true` for Sharon/Poh Li real emails on Railway
