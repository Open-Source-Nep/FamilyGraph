# Authentication

FamilyGraph signs users in with Google only (D-008). The first sign-in creates the user's account. An account stores the Google user ID, the email address and whether Google verified it, and nothing else (D-018). Creating an account never creates a person profile.

## Configuration

| Setting | Environment variable | Purpose |
|---|---|---|
| `Authentication:Google:ClientId` | `Authentication__Google__ClientId` | Google OAuth client ID. Without it, sign-in answers 503. |
| `Authentication:Google:ClientSecret` | `Authentication__Google__ClientSecret` | Google OAuth client secret. Never commit it. |
| `Admin:Emails` | `Admin__Emails__0`, `Admin__Emails__1`, ... | Email addresses that get the `Administrator` role. |

The Client never reads these settings or talks to Google. It loads `GET /api/configuration` when it starts, which says only whether Google sign-in is configured, and the sign-in button navigates to `/api/account/sign-in`.

An account gets or loses `Administrator` each time it signs in, by matching its Google-verified email against `Admin:Emails`. A change to the list takes effect at the user's next sign-in. There is no in-app way to promote or demote anyone.

## Creating a Google OAuth client

1. In the Google Cloud console, open **APIs & Services → Credentials** and create an **OAuth client ID** of type **Web application**.
2. Add an authorised redirect URI for each address the app is served from, ending in `/signin-google`:
   - a self-hosted instance: `https://<your-domain>/signin-google`
   - local development: `http://localhost:4200/signin-google`
3. The consent screen needs only the `openid` and `email` scopes.

Behind a reverse proxy that terminates HTTPS, set `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` so the app builds an `https` redirect URI.

## Local development

```bash
dotnet user-secrets set Authentication:Google:ClientId "<client id>" --project src/Server
dotnet user-secrets set Authentication:Google:ClientSecret "<client secret>" --project src/Server
dotnet user-secrets set "Admin:Emails:0" "you@example.org" --project src/Server
```

The database must be set up as in `docs/database.md`. Run `dotnet run --project src/Server` and open `http://localhost:4200`. The Angular dev server forwards `/api` and `/signin-google` to the Server (`src/client/proxy.conf.json`).

## Sessions and CSRF

The session is the HTTP-only cookie `FamilyGraph.Session` (D-011). `GET /api/account` also sets a readable `XSRF-TOKEN` cookie; the Client's `HttpClient` sends it back in the `X-XSRF-TOKEN` header, and the Server rejects any state-changing request without a valid token.
