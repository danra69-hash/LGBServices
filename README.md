# LGBServices

LGB Services platform — Figma Make UI, React frontend, and ASP.NET Core backend.

## Contents

- **LGBApp.Frontend/** — React + Vite UI extracted from the Figma Make file `LGB Services`
- **LGBApp.Backend/** — ASP.NET Core 8 Web API (auth, users, CRM entities, SQL Server / EF Core)
- **docs/** — Architecture diagrams, design links, and the local Figma Make export
  - [DESIGN.md](docs/DESIGN.md) — Figma UI link + collaboration notes
  - [LGB SERVICES.drawio](docs/LGB%20SERVICES.drawio) — architecture diagram
  - [LGB-Services.make](docs/design/LGB-Services.make) — local Figma Make export (source of the frontend)

## Quick start

### Prerequisites

Node.js and the .NET 8 SDK are installed under `~/.local/`. Add them to your PATH:

```bash
source scripts/dev-path.sh
```

To make this permanent, add that line to your `~/.zshrc`.

### 1. Backend

```bash
source scripts/dev-path.sh
cd LGBApp.Backend
dotnet run
```

Swagger UI: `http://localhost:5003/swagger`

**macOS / local dev:** `appsettings.Development.json` uses SQLite (`lgbapp-dev.db` in `LGBApp.Backend/`). Schema is created on startup via `EnsureCreated()`.

**Windows / SQL Server:** set `Database:Provider` to `SqlServer` (or omit it) and configure `DefaultConnection` in `appsettings.json`. Migrations run automatically on startup; or apply manually:

```bash
dotnet ef database update
```

### 2. Frontend

```bash
source scripts/dev-path.sh
cd LGBApp.Frontend
npm install --legacy-peer-deps
npm run dev
```

Open `http://localhost:5173`. The dev server proxies `/api` requests to the backend on port `5003`.

### 3. Sign in

Register a user via Swagger (`POST /api/auth/register`) or the API, then use the **Login Page** tab or **Sign in** button in the header. Set a user's `Role` to `Admin` in the database (or via Admin user management) to access the Admin tab.

## Design (Figma)

The UI prototype **LGB Services** was built in [Figma Make](https://www.figma.com/make). The exported `.make` file is stored in `docs/design/` and was used to generate `LGBApp.Frontend/`. See [docs/DESIGN.md](docs/DESIGN.md) for collaboration notes and the live Figma project link.

## Integration

| Layer | Role |
|-------|------|
| Figma Make | UI design + interactive prototype |
| LGBApp.Frontend | Production React app (extracted from `.make`, wired to API) |
| LGBApp.Backend | Auth, users, customers, products, jobs, forms, dashboard |

## API endpoints

### Auth
- `POST /api/auth/login` — Sign in (returns JWT + user profile)
- `POST /api/auth/register` — Register new user (public; creates `User` role)

### Users (JWT required; Admin for list/create/update/delete)
- `GET /api/users` — List all users
- `GET /api/users/me` — Current user profile
- `GET /api/users/{id}` — Get user by ID
- `POST /api/users` — Create user with role (Admin)
- `PUT /api/users/{id}` — Update user
- `DELETE /api/users/{id}` — Delete user

### Customers (JWT required)
- `GET /api/customers?search=` — List/search customers
- `GET /api/customers/{id}` — Get customer
- `POST /api/customers` — Create customer
- `PUT /api/customers/{id}` — Update customer
- `DELETE /api/customers/{id}` — Delete customer

### Products (JWT required; Admin for create/update/delete)
- `GET /api/products` — List products
- `GET /api/products/{id}` — Get product
- `POST /api/products` — Create product (Admin)
- `PUT /api/products/{id}` — Update product (Admin)
- `DELETE /api/products/{id}` — Delete product (Admin)

### Job requests (JWT required)
- `GET /api/jobrequests` — Active jobs (Pending / In Progress)
- `GET /api/jobrequests/{id}` — Get job
- `POST /api/jobrequests` — Create job
- `PUT /api/jobrequests/{id}` — Update job (completing/canceling copies to completed services)
- `DELETE /api/jobrequests/{id}` — Delete job (Admin)

### Completed services (JWT required)
- `GET /api/completedservices?search=&year=` — List completed/canceled services

### Forms (JWT required)
- `GET /api/moiforms?jobId=` — List MOI forms
- `POST /api/moiforms` — Create MOI form
- `PUT /api/moiforms/{id}` — Update MOI form
- `DELETE /api/moiforms/{id}` — Delete MOI form (Admin)
- `GET /api/moaforms` — List MOA forms
- `POST /api/moaforms` — Create MOA form
- `PUT /api/moaforms/{id}` — Update MOA form
- `DELETE /api/moaforms/{id}` — Delete MOA form (Admin)

### Dashboard (JWT required)
- `GET /api/dashboard/stats` — Aggregate KPIs for dashboard cards
