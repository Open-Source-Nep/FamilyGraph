import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  Router,
  RouterStateSnapshot,
  UrlTree,
  convertToParamMap,
  provideRouter,
} from '@angular/router';

import { Account } from './account';
import { AuthService } from './auth.service';
import { signedOutGuard } from './signed-out.guard';

describe('signedOutGuard', () => {
  let load: () => Promise<Account | null>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthService, useValue: { load: () => load() } }],
    });
  });

  const maya: Account = {
    id: '1',
    email: 'maya@example.org',
    emailVerified: true,
    roles: ['User'],
  };

  const run = (returnUrl?: string) =>
    TestBed.runInInjectionContext(() =>
      signedOutGuard(
        {
          queryParamMap: convertToParamMap(returnUrl ? { returnUrl } : {}),
        } as ActivatedRouteSnapshot,
        {} as RouterStateSnapshot,
      ),
    );

  const serialize = (result: unknown) => TestBed.inject(Router).serializeUrl(result as UrlTree);

  it('shows the sign-in page to signed-out users', async () => {
    load = () => Promise.resolve(null);

    expect(await run('/account')).toBe(true);
  });

  it('sends signed-in users to where they were going', async () => {
    load = () => Promise.resolve(maya);

    expect(serialize(await run('/people?tab=1'))).toBe('/people?tab=1');
  });

  it('sends signed-in users home instead of off the site', async () => {
    load = () => Promise.resolve(maya);

    expect(serialize(await run('//evil.example.org'))).toBe('/');
  });

  it('shows the sign-in page when the account cannot be loaded', async () => {
    load = () => Promise.reject(new Error('offline'));

    expect(await run()).toBe(true);
  });
});
