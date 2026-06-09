# LGBServices

LGB Services platform — backend API and project documentation.

## Contents

- **LGBApp.Backend/** — ASP.NET Core 8 Web API (users, auth, SQL Server / EF Core)
- **docs/** — Architecture diagrams and design links
  - [DESIGN.md](docs/DESIGN.md) — Figma UI link + collaboration notes
  - [LGB SERVICES.drawio](docs/LGB%20SERVICES.drawio) — architecture diagram

## Design (Figma)

The UI prototype **LGB Services** is in [Figma](https://www.figma.com/files/team/1635583274601385491/project/600229476?fuid=1635583272997894548). See [docs/DESIGN.md](docs/DESIGN.md) for collaboration notes.

## Backend

```bash
cd LGBApp.Backend
dotnet run
```

Swagger UI: `http://localhost:5003/swagger`

## Database

Update the connection string in `LGBApp.Backend/appsettings.json`, then run migrations:

```bash
dotnet ef database update
```
