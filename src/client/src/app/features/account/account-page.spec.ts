import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { Account } from '../../core/auth/account';
import { AuthService } from '../../core/auth/auth.service';
import { AccountPage } from './account-page';

describe('AccountPage', () => {
  const account = signal<Account | null>({
    id: 'a',
    email: 'maya@example.org',
    emailVerified: true,
    roles: ['User'],
  });
  const isAdministrator = signal(false);
  let fixture: ComponentFixture<AccountPage>;

  beforeEach(() => {
    isAdministrator.set(false);
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: AuthService,
          useValue: { account, isAdministrator, signOut: () => Promise.resolve() },
        },
      ],
    });
    fixture = TestBed.createComponent(AccountPage);
  });

  const exportLink = () =>
    (fixture.nativeElement as HTMLElement).querySelector('a[href="/api/export"]');

  it('offers the data export to administrators', async () => {
    isAdministrator.set(true);
    await fixture.whenStable();

    expect(exportLink()?.textContent?.trim()).toBe('Download data export');
    expect(exportLink()?.hasAttribute('download')).toBe(true);
  });

  it('does not offer the data export or import to other users', async () => {
    await fixture.whenStable();

    expect(exportLink()).toBeNull();
    expect((fixture.nativeElement as HTMLElement).querySelector('app-data-import')).toBeNull();
  });

  it('offers the data import to administrators', async () => {
    isAdministrator.set(true);
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).querySelector('app-data-import')).not.toBeNull();
  });
});
