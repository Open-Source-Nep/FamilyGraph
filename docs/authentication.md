# Authentication

FamilyGraph signs users in with Google only (D-008). The first sign-in creates the user's account. An account stores the Google user ID, the email address and whether Google verified it, and nothing else (D-018). Creating an account never creates a person profile.

## Configuration

| Setting | Environment variable | Purpose |
|---|---|---|
| `Authentication:Google:ClientId` | `Authentication__Google__ClientId` | Google OAuth client ID. It is public. Without it, sign-in answers 503. |
| `Admin:Emails` | `Admin__Emails__0`, `Admin__Emails__1`, ... | Email addresses that get the `Administrator` role. |

There is no client secret: the Server never calls Google's token endpoint. The Client loads `GET /api/configuration` when it starts, which says whether Google sign-in is configured and gives the client ID.

## Sign-in flow

1. The sign-in button sends the browser to Google's authorization endpoint with the client ID, `response_type=id_token`, `scope=openid email`, the redirect URI `<origin>/google-callback`, a random `state` and a `nonce`. The nonce is the base64url SHA-256 of a random secret. The Client keeps `state`, the secret and the `returnUrl` in session storage.
2. Google redirects back to the Client page `/google-callback` with the ID token and `state` in the URL fragment, which never reaches a server.
3. That page checks `state` against session storage (each pending sign-in is used once), loads `GET /api/account` for the antiforgery cookie, and posts `{ idToken, nonceSecret }` to `POST /api/account/google-sign-in`. It then replaces the URL so the token leaves the history.
4. The Server validates the ID token with `Google.Apis.Auth`: signature against Google's keys, issuer, expiry and audience (the client ID). It also checks that the SHA-256 of `nonceSecret` is the token's nonce, so a leaked token cannot be replayed without the secret. It registers or updates the account, starts the session and answers 204. A rejected token answers 401 `account.sign_in_failed`.
5. The page loads the account and navigates to `returnUrl`, or to `/sign-in?failed=true` on any failure.

An account gets or loses `Administrator` each time it signs in, by matching its Google-verified email against `Admin:Emails`. A change to the list takes effect at the user's next sign-in. There is no in-app way to promote or demote anyone.

## Creating a Google OAuth client

1. In the Google Cloud console, open **APIs & Services → Credentials** and create an **OAuth client ID** of type **Web application**.
2. Add an authorised redirect URI for each address the app is served from, ending in `/google-callback`:
   - a self-hosted instance: `https://<your-domain>/google-callback`
   - local development: `https://localhost:4200/google-callback`
3. The consent screen needs only the `openid` and `email` scopes.

The Client builds the redirect URI from the address in the browser, so it must match one registered here exactly.

## Local development

```bash
dotnet user-secrets set Authentication:Google:ClientId "<client id>" --project src/Server
dotnet user-secrets set "Admin:Emails:0" "you@example.org" --project src/Server
```

The database must be set up as in `docs/database.md`. Run `dotnet run --project src/Server` and open `https://localhost:4200`. The Angular dev server serves HTTPS with the ASP.NET Core development certificate (`src/client/aspnetcore-https.js` exports it on first start). It serves `/google-callback` itself and forwards `/api` and `/hubs` to the Server (`src/client/src/proxy.conf.js`): `https://localhost:$ASPNETCORE_HTTPS_PORT` when that is set, otherwise the first `ASPNETCORE_URLS` entry, otherwise `http://localhost:5039`. Sign-in does not depend on the proxy, because the Client builds the redirect URI from the browser's origin. Open the app on `localhost`, not `127.0.0.1`: Google matches the redirect URI exactly.

## Sessions and CSRF

The session is the HTTP-only cookie `FamilyGraph.Session` (D-011). `GET /api/account` also sets a readable `XSRF-TOKEN` cookie; the Client's `HttpClient` sends it back in the `X-XSRF-TOKEN` header, and the Server rejects any state-changing request without a valid token.
