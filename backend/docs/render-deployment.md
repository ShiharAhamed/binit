# Render deployment: ASP.NET Core API

Deploy only the backend as a Render **Web Service** using the **Docker** runtime. The Docker build context is `backend/`, which contains the four referenced source projects. The image runs the published API and its existing hosted workflow worker. It does not run EF migrations.

## Service settings

| Setting | Value |
| --- | --- |
| Repository / branch | This repository / `main` |
| Service type / runtime | Web Service / Docker |
| Root Directory | `backend` |
| Dockerfile Path | `./Dockerfile` (relative to Root Directory) |
| Docker build context | `.` (relative to Root Directory) |
| Docker Command | Leave empty; use the Dockerfile `CMD` |
| Instance | Free |
| Health Check Path | `/health` |
| Region | Choose the available region nearest your users and Supabase project |
| Auto Deploy | Enable from `main` after reviewing and pushing these files |

The container listens on `0.0.0.0:${PORT:-10000}`. Render supplies `PORT=10000` by default; the Dockerfile also works if Render sets another port. Set `ASPNETCORE_ENVIRONMENT=Production` explicitly in Render. The Dockerfile supplies the same default. Logs go to stdout/stderr.

## Environment variables

Enter these in the **backend Web Service** Environment panel. Never put real values in the Dockerfile or repository. Nested .NET settings use `__`.

| Variable | Required? | Secret? | Purpose / example format |
| --- | --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | Yes | No | `Production`; excludes development users and Swagger. |
| `PORT` | No | No | Render supplies `10000` by default; Docker CMD also defaults to `10000`. |
| `ConnectionStrings__DefaultConnection` | Yes | Yes | Supabase PostgreSQL ADO.NET string: `Host=<SUPABASE_DB_HOST>;Port=5432;Database=<DB_NAME>;Username=<DB_USER>;Password=<SUPABASE_DB_PASSWORD>;SSL Mode=Require`. Use the Supabase provided connection details, including its actual host, port, and SSL requirements. |
| `Jwt__Key` | Yes | Yes | New strong production signing secret: `<GENERATE_STRONG_SECRET>`. The code has a development fallback if omitted, so set this explicitly. |
| `Jwt__Issuer` | No, default exists | No | Token issuer; default `SmartWaste.Api`. Keep consistent with clients. |
| `Jwt__Audience` | No, default exists | No | Token audience; default `SmartWaste.Clients`. Keep consistent with clients. |
| `Jwt__ExpiryMinutes` | No, default exists | No | Access token lifetime; default `60`. |
| `AiService__BaseUrl` | Yes for AI workflows | No | Deployed FastAPI HTTPS base URL: `https://<AI_SERVICE_HOST>`. Do not use the localhost default. |
| `AiService__TimeoutSeconds` | No, default exists | No | AI request timeout; default `300`. Must remain below the workflow lease with grace. |
| `InternalService__ApiKey` | Yes for AI integration | Yes | Shared key: `<GEMINI_INTERNAL_SHARED_KEY>`. Must match FastAPI's internal service key. This key is also used to authenticate FastAPI callbacks to ASP.NET Core. |
| `Storage__Supabase__BaseUrl` | Yes for report attachments | No | Supabase project URL: `https://<SUPABASE_PROJECT>.supabase.co`. |
| `Storage__Supabase__SecretKey` | Yes for report attachments | Yes | Supabase server-side secret/service-role key: `<SUPABASE_STORAGE_SECRET>`. Backend only; never expose to React or Flutter. |
| `Storage__Supabase__Bucket` | No, default exists | No | Private bucket; default `waste-report-attachments`. |
| `Storage__Supabase__SignedUrlExpirySeconds` | No, default exists | No | Signed URL duration; default `900`. |
| `Municipality__TimeZoneId` | No, default exists | No | Collection schedule timezone; default `Asia/Colombo`. |
| `ReportTriggeredWorkflowWorker__Enabled` | No, default exists | No | Default `true`; keep enabled for the existing worker. |
| `ReportTriggeredWorkflowWorker__PollIntervalSeconds` | No, default exists | No | Default `15`. |
| `ReportTriggeredWorkflowWorker__LeaseSeconds` | No, default exists | No | Default `420`; must exceed AI timeout plus 30 seconds. |
| `ReportTriggeredWorkflowWorker__RetryDelaySeconds` | No, default exists | No | Default `60`. |
| `ReportTriggeredWorkflowWorker__MaxAttempts` | No, default exists | No | Default `3`. |

There is **no configuration key for production CORS origins**. `Program.cs` currently permits only `http://localhost:3000`, `http://localhost:5173`, and `http://localhost:8080`. This does not block backend-only deployment or direct API calls, but a deployed React browser app will fail cross-origin requests. A later, separately approved `Program.cs` change is required once the React origin is known. Do not set a guessed CORS variable; the current code will not read it.

The report upload form limit is fixed at 10 MiB in the controller; no environment setting exists. `Seed__DevUserPassword` is only consulted in Development and must not be set on Render.

## Deployment sequence

1. Review the Dockerfile, `.dockerignore`, this guide, and the CORS blocker. Push the reviewed files to `main` yourself.
2. Open Render → **New** → **Web Service**, connect this GitHub repository, and select `main`.
3. Choose **Docker**, set the Root Directory, Dockerfile Path, and context as above, select **Free**, and choose a region.
4. Add the required variables above as Render environment variables. Use new production JWT and internal service secrets. Keep Supabase credentials only in the backend service.
5. Set the health check path to `/health`, enable Auto Deploy if desired, and deploy.
6. Read startup logs for successful binding on `0.0.0.0:10000` (or Render's `PORT`), seeding warnings, DB connectivity, and worker warnings. Open `https://<BACKEND_HOST>/health`; it should return HTTP 200 with `{"status":"healthy"}`. This endpoint checks the API process, not DB or AI readiness.
7. Arrange EF migrations separately as an explicit admin operation before relying on database-backed endpoints. The container does not run migrations.
8. Deploy the AI service separately, set `AiService__BaseUrl`, and use the same internal key on both services. After the React URL exists, resolve the CORS blocker before browser integration.

Production startup still attempts **idempotent Identity role seeding** through the existing application code. It does not create development test users in Production, reset the database, or run migrations. Local packaging checks must use an isolated or dummy database connection, never production Supabase.

On Render Free, the web service can sleep after inactivity and the first request can be slow. The hosted report workflow worker runs only while the API process is awake; retries resume only when the service is running. No keep-alive service is configured.
