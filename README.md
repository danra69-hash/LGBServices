# LGBServices

LGB Services platform — backend API and project documentation.

## Contents

- **LGBApp.Backend/** — ASP.NET Core 8 Web API (users, auth, SQL Server / EF Core)
- **docs/** — Architecture diagrams and project documentation

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
