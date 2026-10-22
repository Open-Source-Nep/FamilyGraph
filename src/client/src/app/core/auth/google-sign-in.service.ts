import { DOCUMENT } from '@angular/common';
import { InjectionToken, Service, inject } from '@angular/core';

import { localReturnUrl } from './account';

/** Google's OAuth 2.0 authorization endpoint (OpenID Connect). */
export const GOOGLE_AUTHORIZATION_ENDPOINT = 'https://accounts.google.com/o/oauth2/v2/auth';

/** The Client route Google redirects to; it is the URI registered in the Google console. */
export const GOOGLE_CALLBACK_PATH = '/google-callback';

/** The part of `window.location` sign-in uses, so tests can replace it. */
export const BROWSER_LOCATION = new InjectionToken<Pick<Location, 'origin' | 'assign'>>(
  'BROWSER_LOCATION',
  { factory: () => inject(DOCUMENT).location },
);

/** What the callback page sends to the server once Google has signed the user in. */
export interface GoogleSignInResult {
  idToken: string;
  nonceSecret: string;
  returnUrl: string;
}

interface PendingSignIn {
  state: string;
  nonceSecret: string;
  returnUrl: string;
}

const pendingKey = 'familygraph.google-sign-in';

/**
 * Runs the Google side of sign-in in the browser (docs/authentication.md). It sends the browser to
 * Google for an ID token with `state` and a `nonce`, and reads the token from the callback URL. The
 * nonce is the SHA-256 of a secret kept in session storage; the server checks the token and that
 * the secret matches its nonce.
 */
@Service()
export class GoogleSignInService {
  private readonly location = inject(BROWSER_LOCATION);

  /** Leaves the app for Google's sign-in page. */
  async start(clientId: string, returnUrl: string): Promise<void> {
    const pending: PendingSignIn = {
      state: randomToken(),
      nonceSecret: randomToken(),
      returnUrl: localReturnUrl(returnUrl),
    };
    sessionStorage.setItem(pendingKey, JSON.stringify(pending));

    const params = new URLSearchParams({
      client_id: clientId,
      redirect_uri: this.location.origin + GOOGLE_CALLBACK_PATH,
      response_type: 'id_token',
      // An account keeps only the Google ID and email (D-018).
      scope: 'openid email',
      state: pending.state,
      nonce: await sha256Base64Url(pending.nonceSecret),
    });
    this.location.assign(`${GOOGLE_AUTHORIZATION_ENDPOINT}?${params}`);
  }

  /**
   * Reads Google's answer from the callback URL fragment. `null` when Google returned an error,
   * the `state` does not match the sign-in this browser started, or there is no token. The pending
   * sign-in is used up either way.
   */
  complete(fragment: string | null): GoogleSignInResult | null {
    const stored = sessionStorage.getItem(pendingKey);
    sessionStorage.removeItem(pendingKey);
    if (!stored) {
      return null;
    }

    const pending = JSON.parse(stored) as PendingSignIn;
    const answer = new URLSearchParams(fragment ?? '');
    const idToken = answer.get('id_token');
    if (answer.get('state') !== pending.state || !idToken) {
      return null;
    }

    return {
      idToken,
      nonceSecret: pending.nonceSecret,
      returnUrl: localReturnUrl(pending.returnUrl),
    };
  }
}

function randomToken(): string {
  return base64Url(crypto.getRandomValues(new Uint8Array(32)));
}

/** Base64url without padding, as the server computes it. */
export async function sha256Base64Url(value: string): Promise<string> {
  const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(value));
  return base64Url(new Uint8Array(digest));
}

function base64Url(bytes: Uint8Array): string {
  return btoa(String.fromCharCode(...bytes))
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '');
}
