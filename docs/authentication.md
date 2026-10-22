# Authentication

FamilyGraph signs users in with Google only (D-008). The first sign-in creates the user's account. An account stores the Google user ID, the email address and whether Google verified it, and nothing else (D-018). Creating an account never creates a person profile.

## Configuration

| Setting | Environment variable | Purpose |
|---|---|---|
| `Authentication:Google:ClientId` | `Authentication__Google__ClientId` | Google OAuth client ID. Without it, sign-in answers 503. |
| `Authentication:Google:ClientSecret` | `Authentication__Google__ClientSecret` | Google OAuth client secret. Never commit it. |
| `Admin:Emails` | `Admin__Emails__0`, `Admin__Emails__1`, ... | Email addresses that get the `Administrator` role. |

The Client never reads these settings or talks to Google. It loads `GET /api/configuration` when it starts, which says only whether Google sign-in is configured, and the sign-in button navigates to `/api/account/sign-in`.

## Sign-in flow

1. The sign-in button navigates to `GET /api/account/sign-in?returnUrl=`, which redirects to Google.
2. Google redirects back to the Client page `/google-callback` with its one-time code and `state`.
3. That page calls `GET /api/account/google-callback` with the same query string. The Server's Google handler checks `state`, exchanges the code (with `/google-callback` as the redirect URI, as Google requires) and redirects to `GET /api/account/sign-in/complete`, which registers or updates the account, starts the session and answers with the local `returnUrl`. A failed or cancelled sign-in answers 401 `account.sign_in_failed`.
4. The page loads the account and navigates to `returnUrl`, or to `/sign-in?failed=true` on failure.

The Client only relays Google's query string; every check runs on the Server.

An account gets or loses `Administrator` each time it signs in, by matching its Google-verified email against `Admin:Emails`. A change to the list takes effect at the user's next sign-in. There is no in-app way to promote or demote anyone.

## Creating a Google OAuth client

1. In the Google Cloud console, open **APIs & Services → Credentials** and create an **OAuth client ID** of type **Web application**.
2. Add an authorised redirect URI for each address the app is served from, ending in `/google-callback`:
   - a self-hosted instance: `https://<your-domain>/google-callback`
   - local development: `https://localhost:4200/google-callback`
3. The consent screen needs only the `openid` and `email` scopes.

Behind a reverse proxy that terminates HTTPS, set `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` so the app builds an `https` redirect URI.

## Local development

```bash
dotnet user-secrets set Authentication:Google:ClientId "<client id>" --project src/Server
dotnet user-secrets set Authentication:Google:ClientSecret "<client secret>" --project src/Server
dotnet user-secrets set "Admin:Emails:0" "you@example.org" --project src/Server
```

The database must be set up as in `docs/database.md`. Run `dotnet run --project src/Server` and open `https://localhost:4200`. The Angular dev server serves HTTPS with the ASP.NET Core development certificate (`src/client/aspnetcore-https.js` exports it on first start). It serves `/google-callback` itself and forwards only `/api` to the Server over HTTP (`src/client/proxy.conf.json`) with `X-Forwarded-*` headers, and the Development profiles set `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`, so the Server builds an `https` redirect URI. Open the app on `localhost`, not `127.0.0.1`: Google matches the redirect URI exactly.

## Sessions and CSRF

The session is the HTTP-only cookie `FamilyGraph.Session` (D-011). `GET /api/account` also sets a readable `XSRF-TOKEN` cookie; the Client's `HttpClient` sends it back in the `X-XSRF-TOKEN` header, and the Server rejects any state-changing request without a valid token.
