# Go live (simple path)

Frontend is already on Vercel. This gets the **API** online so Sign In works.

Skip Supabase for now. Use **Railway** (free trial / hobby) + SQLite — good enough to go live and demo.

## 1. Deploy the API (Railway)

1. Go to [railway.app](https://railway.app) → login with GitHub  
2. **New Project** → **Deploy from GitHub repo** → `Ryannnism/LGBTesting`  
3. It should pick up the root `Dockerfile`  
4. Add a **Volume** mounted at `/data` (keeps the SQLite DB)  
5. **Variables** (Settings → Variables):

```
ASPNETCORE_ENVIRONMENT=Production
Database__Provider=Sqlite
ConnectionStrings__DefaultConnection=Data Source=/data/lgbapp.db
Jwt__Key=<paste-a-long-random-string-at-least-32-chars>
Jwt__Issuer=LGBApp.Backend
Jwt__Audience=LGBApp.Frontend
Cors__AllowedOrigins__0=https://lgb-testing.vercel.app
DISABLE_HTTPS_REDIRECTION=true
AllowedHosts=*
App__PublicFrontendUrl=https://lgb-testing.vercel.app
Email__From=LGB Services <noreply@your-verified-domain.com>
Email__ResendApiKey=<your-resend-api-key>
```

6. **Settings → Networking → Generate Domain**  
   Copy the URL, e.g. `https://lgbtesting-production-xxxx.up.railway.app`

First boot may take 1–2 minutes while it seeds the DB.

### Email (forgot password OTP + MOI/MOA alerts)

- Create a free [Resend](https://resend.com) account, verify a sending domain (or use `onboarding@resend.dev` only for testing to your own inbox).
- Set `Email__ResendApiKey` and `Email__From` as above.
- **Without** `Email__ResendApiKey`, the API still works: OTP codes and approval emails are **written to Railway logs** only (logging sink). Useful for local/dev.

## 2. Point Vercel at the API

1. Vercel → project → **Settings → Environment Variables**  
2. Set:

```
VITE_API_BASE=https://YOUR-RAILWAY-URL.up.railway.app
```

(no trailing slash)

3. **Redeploy** the frontend  

## 3. Smoke test

1. Open https://lgb-testing.vercel.app  
2. Sign in with a seeded account (SQLite seeds staff), e.g. `sharon@lgb.test` / `password123`  
3. You’ll be asked to change password on first login  
4. **Forgot password:** request a code → check email (or Railway logs if Resend is unset) → reset  
5. Push an MOI/MOA for client approval → signatory gets in-app + email notice  

## Later (real production)

- Move DB to Azure SQL / Postgres  
- Host API on Azure App Service  
- See [DEPLOYMENT.md](../DEPLOYMENT.md)  

Supabase can wait until you want storage/auth migration — not needed to go live.
