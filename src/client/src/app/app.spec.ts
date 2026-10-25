import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { computed, signal } from '@angular/core';

import { App } from './app';
import { Account } from './core/auth/account';
import { AuthService } from './core/auth/auth.service';

describe('App', () => {
  const account = signal<Account | null>(null);
  const isAdministrator = computed(() => account()?.roles.includes('Administrator') ?? false);

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { account, isAdministrator } },
      ],
    }).compileComponents();
  });

  it('shows the people and account links only when signed in', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('nav')).toBeNull();

    account.set({ id: '1', email: 'maya@example.org', emailVerified: true, roles: ['User'] });
    await fixture.whenStable();
    const links = [...element.querySelectorAll('nav a')].map((link) => link.textContent);
    expect(links).toEqual(['People', 'Approvals', 'Account']);
  });

  it('shows the Users link to administrators', async () => {
    const fixture = TestBed.createComponent(App);
    account.set({
      id: '1',
      email: 'admin@example.org',
      emailVerified: true,
      roles: ['User', 'Administrator'],
    });
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    const links = [...element.querySelectorAll('nav a')].map((link) => link.textContent);
    expect(links).toEqual(['People', 'Approvals', 'Users', 'Account']);
  });
});
