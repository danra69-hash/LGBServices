# Deploy frontend to Vercel + connect Supabase

This app is **React/Vite + .NET 8**, not Next.js. Use the Vite Supabase client (`@supabase/supabase-js`), not `@supabase/ssr`.

## What goes where

| Piece | Host |
|-------|------|
| Frontend (`LGBApp.Frontend`) | **Vercel** |
| API (`LGBApp.Backend`) | Azure App Service / VM / similar (not Vercel) |
| App database | **SQL Server / Azure SQL** (existing EF migrations) |
| Supabase | Client SDK for future auth/storage features |

Supabase Postgres cannot replace the app DB yet — migrations use SQL Server types (`nvarchar`, `GETUTCDATE()`, identities).

## 1. Supabase project

You already have a project. Local frontend env (gitignored):

```
VITE_SUPABASE_URL=https://YOUR_PROJECT.supabase.co
VITE_SUPABASE_PUBLISHABLE_KEY=sb_publishable_...
```

Use the same names in Vercel → Project Settings → Environment Variables.

## 2. Deploy frontend on Vercel

1. Import the GitHub repo in [vercel.com](https://vercel.com).
2. Set **Root Directory** to `LGBApp.Frontend`.
3. Framework: Vite (auto-detected). Build: `npm run build`, output: `dist`.
4. Add env vars:
   - `VITE_SUPABASE_URL`
   - `VITE_SUPABASE_PUBLISHABLE_KEY`
   - `VITE_API_BASE` = your public API URL (e.g. `https://api.yourdomain.com`)
5. Deploy.

`vercel.json` rewrites unknown paths to `index.html` for SPA routing.

## 3. Deploy the API

Follow [DEPLOYMENT.md](../DEPLOYMENT.md). Point CORS at your Vercel URL:

```
Cors__AllowedOrigins__0=https://your-app.vercel.app
```

## 4. Using Supabase in code

```ts
import { supabase } from '@/lib/supabase';
```

Auth and business data still go through the .NET API (`@/lib/api`) until you intentionally migrate those flows.
