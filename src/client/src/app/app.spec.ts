import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { signal } from '@angular/core';

import { App } from './app';
import { Account } from './core/auth/account';
import { AuthService } from './core/auth/auth.service';

describe('App', () => {
  const account = signal<Account | null>(null);

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([]), { provide: AuthService, useValue: { account } }],
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
});
