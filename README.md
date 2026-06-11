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

## Default accounts (Development)

All seeded passwords: **`password123`** (must change on first login).

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

## Production notes

- Set `Database:Provider` to `SqlServer` and configure `ConnectionStrings:DefaultConnection` in `appsettings.json`.
- Change `Jwt:Key` and use secrets management — never commit production credentials.
- `lgbapp-dev.db`, `lgbapp.db`, and all `node_modules/` folders are intentionally **not** in git.

## Further reading

- [docs/USER_ROLES.md](docs/USER_ROLES.md) — role hierarchy and MOI/MOA pipeline
