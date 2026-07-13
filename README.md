# LGB Services

MOI/MOA workflow app for LGB secretarial teams and client companies (.NET 8 API + React/Vite frontend).

## Prerequisites

| Tool | Version |
|------|---------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 8.0+ |
| [Node.js](https://nodejs.org/) | 18+ (20 LTS recommended) |

No SQL Server required for local dev — the API uses **SQLite** automatically in Development.

## Quick start

```bash
git clone https://github.com/danra69-hash/LGBServices.git
cd LGBServices

# 1. Backend (creates & seeds the database on first run)
cd LGBApp.Backend
dotnet run
# API: http://localhost:5003  Swagger: http://localhost:5003/swagger

# 2. Frontend (new terminal)
cd ../LGBApp.Frontend
npm install
npm run dev
# UI: http://localhost:5173
```

Open **http://localhost:5173**, log in with any account below (password **`password123`**). You will be prompted to change the password on first login.

The Vite dev server proxies `/api` → `http://localhost:5003`. To point at another API host, copy `.env.example` to `.env` and set `VITE_API_BASE`.

## What happens on first backend run

You do **not** need to commit or download a database file. On startup the API:

1. Creates `LGBApp.Backend/lgbapp-dev.db` (gitignored)
2. Applies SQLite schema migrations
3. Seeds demo customer **Acme Corp** with **Enterprise Plus** package
4. Seeds internal LGB staff, form templates, and product catalog
5. Syncs package service lines into jobs (board meetings, resolutions, etc.)
6. Provisions MOI/MOA forms and repairs multi-session integrity

To **reset** local data: stop the API, delete `LGBApp.Backend/lgbapp-dev.db`, and run `dotnet run` again.

## Initial customer data (Sharon)

Excel workbook and import guide for loading existing customers:

- **[docs/INITIAL_DATA_COLLECTION.md](docs/INITIAL_DATA_COLLECTION.md)** — what to collect vs auto-seeded
- **[docs/templates/LGB_Customer_Initialization_Template.xlsx](docs/templates/LGB_Customer_Initialization_Template.xlsx)** — fill-in template (plain-language sheets)
- Regenerate: `python3 scripts/build_sharon_init_template.py`
- Import: `python3 scripts/import-sharon-workbook.py --email sharon@lgb.test`

## Default accounts (Development)

All seeded passwords: **`password123`** (must change on first login).

On first SQLite startup the API also loads **CubeV COSEC Billing Tracking** (`Data/Seed/cubev-init.json`) — ~169 real companies, billing parties, division recommenders, and approval contacts. Re-seed: delete `LGBApp.Backend/lgbapp-dev.db` and restart. Source workbook: `docs/source/COSEC_Billing_Tracking_2026_CubeV.xlsx`.

### Internal (LGB)

| Email | Role | Notes |
|-------|------|-------|
| `sharon@lgb.test` | Admin | MOI intake, assignment, sign-off |
| `ngpohli@lgb.test` | User | Resolution prep |
| `nita@lgb.test` | User | Resolution prep |
| `siti@lgb.test` | User | Resolution prep |
| `nadia@lgb.test` | User | Resolution prep |

### Client — Acme Corp (demo company)

| Email | Role | Notes |
|-------|------|-------|
| `clientadmin@acme.test` | Client Admin | Issues MOI, sets dates, invites team |
| `dra@lgb.test` | Client Signatory | MOI issuer (Dan Ra) |
| `dra2@lgb.test` | Client Signatory | MOI issuer + MOI approver (Daniel Ra) |
| `dra3@lgb.test` | Client Signatory | MOA signatory |

**Enterprise Plus** includes add-ons such as **Attend Board Meeting** (4 sessions) and **Prepare board meeting Minutes** (2 sessions) — useful for testing per-session MOI/MOA and category counters on the client portal.

## Project layout

```
LGBServices/
├── LGBApp.Backend/     # ASP.NET Core API (SQLite in dev)
├── LGBApp.Frontend/    # React + Vite UI
└── docs/               # Role docs & diagrams (optional PDF build)
```

## Optional: rebuild role documentation PDFs

Diagram sources live in `docs/diagrams/`. To regenerate PDFs you need Node tooling locally (not required to run the app):

```bash
cd docs
npm install @mermaid-js/mermaid-cli   # if not already installed
python3 build-user-roles-pdf.py
```

## Production / go-live

Local dev uses SQLite and seeded demo accounts. For a real deployment (SQL Server, HTTPS, many users, persistent uploads), follow **[docs/DEPLOYMENT.md](docs/DEPLOYMENT.md)**.

For **Vercel (frontend) + Supabase (client SDK)**, see **[docs/deploy/vercel-supabase.md](docs/deploy/vercel-supabase.md)**.

Quick pointers:

- Set `ASPNETCORE_ENVIRONMENT=Production` and `Database:Provider` to `SqlServer`
- Override `ConnectionStrings:DefaultConnection` and `Jwt:Key` via environment variables (see `appsettings.Production.json`)
- Build the UI with `npm run build`; sample nginx and Azure configs live in `docs/deploy/`
- `lgbapp-dev.db`, `uploads/`, and all `node_modules/` folders are intentionally **not** in git

## Further reading

- [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md) — production deployment runbook
- [docs/USER_ROLES.md](docs/USER_ROLES.md) — role hierarchy and MOI/MOA pipeline
