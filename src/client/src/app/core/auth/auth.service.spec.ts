import { HttpParams, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { Account } from './account';
import { AuthService } from './auth.service';

const maya: Account = { id: '1', email: 'maya@example.org', emailVerified: true, roles: ['User'] };

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads the signed-in account once', async () => {
    const first = service.load();
    const second = service.load();
    http.expectOne('/api/account').flush(maya);

    expect(await first).toEqual(maya);
    expect(await second).toEqual(maya);
    expect(service.account()).toEqual(maya);
    expect(service.isAdministrator()).toBe(false);
  });

  it('treats 401 as signed out', async () => {
    const loaded = service.load();
    http.expectOne('/api/account').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(await loaded).toBeNull();
    expect(service.account()).toBeNull();
  });

  it('recognises administrators', async () => {
    const loaded = service.load();
    http.expectOne('/api/account').flush({ ...maya, roles: ['User', 'Administrator'] });
    await loaded;

    expect(service.isAdministrator()).toBe(true);
  });

  it('signs out with a POST and forgets the account', async () => {
    const loaded = service.load();
    http.expectOne('/api/account').flush(maya);
    await loaded;

    const signedOut = service.signOut();
    const request = http.expectOne('/api/account/sign-out');
    expect(request.request.method).toBe('POST');
    request.flush(null, { status: 204, statusText: 'No Content' });
    await signedOut;

    expect(service.account()).toBeNull();
    expect(await service.load()).toBeNull();
  });

  it('completes Google sign-in through the API and loads the new account', async () => {
    const loaded = service.load();
    http.expectOne('/api/account').flush(null, { status: 401, statusText: 'Unauthorized' });
    await loaded;

    const completed = service.completeGoogleSignIn(
      new HttpParams({ fromObject: { code: 'c', state: 's' } }),
    );
    http.expectOne('/api/account/google-callback?code=c&state=s').flush({ returnUrl: '/people' });
    await Promise.resolve();
    http.expectOne('/api/account').flush(maya);

    expect(await completed).toBe('/people');
    expect(service.account()).toEqual(maya);
  });

  it('never returns off the site after Google sign-in', async () => {
    const completed = service.completeGoogleSignIn(new HttpParams());
    http.expectOne('/api/account/google-callback').flush({ returnUrl: '//evil.example.org' });
    await Promise.resolve();
    http.expectOne('/api/account').flush(maya);

    expect(await completed).toBe('/');
  });
});
