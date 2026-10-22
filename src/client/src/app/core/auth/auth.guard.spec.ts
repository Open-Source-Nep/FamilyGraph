import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  Router,
  RouterStateSnapshot,
  UrlTree,
  provideRouter,
} from '@angular/router';

import { Account } from './account';
import { authGuard } from './auth.guard';
import { AuthService } from './auth.service';

describe('authGuard', () => {
  let account: Account | null;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { load: () => Promise.resolve(account) } },
      ],
    });
  });

  const run = () =>
    TestBed.runInInjectionContext(() =>
      authGuard({} as ActivatedRouteSnapshot, { url: '/account?tab=1' } as RouterStateSnapshot),
    );

  it('lets signed-in users through', async () => {
    account = { id: '1', email: 'maya@example.org', emailVerified: true, roles: ['User'] };

    expect(await run()).toBe(true);
  });

  it('sends signed-out users to sign in and back afterwards', async () => {
    account = null;

    const result = (await run()) as UrlTree;

    expect(TestBed.inject(Router).serializeUrl(result)).toBe(
      '/sign-in?returnUrl=%2Faccount%3Ftab%3D1',
    );
  });
});
