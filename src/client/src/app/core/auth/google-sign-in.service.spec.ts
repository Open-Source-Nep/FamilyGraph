import { TestBed } from '@angular/core/testing';

import {
  BROWSER_LOCATION,
  GOOGLE_AUTHORIZATION_ENDPOINT,
  GoogleSignInService,
  sha256Base64Url,
} from './google-sign-in.service';

describe('GoogleSignInService', () => {
  let service: GoogleSignInService;
  let assigned: string[];

  beforeEach(() => {
    sessionStorage.clear();
    assigned = [];
    TestBed.configureTestingModule({
      providers: [
        {
          provide: BROWSER_LOCATION,
          useValue: {
            origin: 'https://family.example.org',
            assign: (url: string) => assigned.push(url),
          },
        },
      ],
    });
    service = TestBed.inject(GoogleSignInService);
  });

  const googleRequest = () => new URL(assigned[0]);

  it('sends the browser to Google for an ID token with only the openid and email scopes', async () => {
    await service.start('client-id', '/people');

    const url = googleRequest();
    expect(`${url.origin}${url.pathname}`).toBe(GOOGLE_AUTHORIZATION_ENDPOINT);
    expect(url.searchParams.get('client_id')).toBe('client-id');
    expect(url.searchParams.get('redirect_uri')).toBe('https://family.example.org/google-callback');
    expect(url.searchParams.get('response_type')).toBe('id_token');
    expect(url.searchParams.get('scope')).toBe('openid email');
    expect(url.searchParams.get('state')).toBeTruthy();
  });

  it('returns the token and the secret behind the nonce when the state matches', async () => {
    await service.start('client-id', '/people');
    const state = googleRequest().searchParams.get('state')!;
    const nonce = googleRequest().searchParams.get('nonce');

    const result = service.complete(`id_token=token&state=${state}`);

    expect(result?.idToken).toBe('token');
    expect(result?.returnUrl).toBe('/people');
    expect(await sha256Base64Url(result!.nonceSecret)).toBe(nonce);
  });

  it('rejects an answer for a sign-in this browser did not start', async () => {
    await service.start('client-id', '/');

    expect(service.complete('id_token=token&state=forged')).toBeNull();
  });

  it('rejects Google errors', async () => {
    await service.start('client-id', '/');
    const state = googleRequest().searchParams.get('state')!;

    expect(service.complete(`error=access_denied&state=${state}`)).toBeNull();
  });

  it('uses a pending sign-in only once', async () => {
    await service.start('client-id', '/');
    const fragment = `id_token=token&state=${googleRequest().searchParams.get('state')}`;
    service.complete(fragment);

    expect(service.complete(fragment)).toBeNull();
  });

  it('never returns off the site', async () => {
    await service.start('client-id', '//evil.example.org');
    const state = googleRequest().searchParams.get('state')!;

    expect(service.complete(`id_token=token&state=${state}`)?.returnUrl).toBe('/');
  });

  it('matches the nonce the server computes', async () => {
    // Base64url SHA-256 of "secret", as in AccountTestFactory.Nonce.
    expect(await sha256Base64Url('secret')).toBe('K7gNU3sdo-OL0wNhqoVWhr3g6s1xYv72ol_pe_Unols');
  });
});
