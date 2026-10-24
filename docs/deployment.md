# Deployment

FamilyGraph ships as one Docker image: the Server with the built Client in `wwwroot`, on one origin (D-011).

## The image

`src/Server/Dockerfile` uses `src/` as the build context:

```bash
docker build -f src/Server/Dockerfile -t familygraph src
docker run -p 8080:8080 --env-file familygraph.env familygraph
```

It has three stages: Node 24 runs the production Angular build, the .NET SDK publishes Server with that build copied into `wwwroot`, and the ASP.NET Core runtime image runs it as the non-root `app` user. The app listens on HTTP port 8080.

Published images are at `ghcr.io/open-source-nep/familygraph`. The **Publish image** workflow (`.github/workflows/publish.yml`) runs only by hand from the Actions tab. It tags each image `latest` and `sha-<commit>`, plus the version you enter (for example `1.2.0`).

## Configuration

All settings come from environment variables (D-012); `__` separates section levels.

| Variable | Purpose |
|---|---|
| `Database__Host`, `Database__Port`, `Database__Database`, `Database__Username`, `Database__Password` | PostgreSQL connection (`docs/database.md`). Any other Npgsql keyword works the same way, such as `Database__SslMode=Require`. |
| `Authentication__Google__ClientId` | Google OAuth client ID (`docs/authentication.md`). Register `https://<your-domain>/google-callback` as its redirect URI. |
| `Admin__Emails__0`, `Admin__Emails__1`, ... | Administrator email addresses. |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` | Set this behind a reverse proxy or host that terminates HTTPS, so the app sees the original `https` scheme and client address. Only set it when the proxy is the only way in. |
| `Graph__*`, `Handovers__LinkLifetimeDays` | Optional limits; the defaults suit most instances. |

HTTPS is terminated by your reverse proxy or host. The session cookie is always `Secure`, so the app must be reached over HTTPS.

## Database migrations

The app never migrates by itself (D-011). The image contains an EF Core migration bundle, `./efbundle`, which applies any pending migrations with the same `Database__*` settings as the app. It is safe to run again on an up-to-date database. Either:

- run it as a one-off step before starting a new version: `docker run --rm --env-file familygraph.env ghcr.io/open-source-nep/familygraph ./efbundle`, or
- set `FAMILYGRAPH_MIGRATE=true`, and the container runs it before every start. A failed migration stops the container, so the old version keeps serving where the host supports zero-downtime deploys. Use this where one-off steps are not available, such as Render's free plan.
