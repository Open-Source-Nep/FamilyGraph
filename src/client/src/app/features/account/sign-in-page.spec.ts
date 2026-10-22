import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ClientConfiguration } from '../../core/config/client-configuration';
import { ConfigurationService } from '../../core/config/configuration.service';
import { SignInPage } from './sign-in-page';

describe('SignInPage', () => {
  const configuration = signal<ClientConfiguration | null>(null);
  let fixture: ComponentFixture<SignInPage>;

  beforeEach(() => {
    configuration.set({ google: { enabled: true } });
    TestBed.configureTestingModule({
      providers: [{ provide: ConfigurationService, useValue: { configuration } }],
    });
    fixture = TestBed.createComponent(SignInPage);
  });

  const element = () => fixture.nativeElement as HTMLElement;
  const link = () => element().querySelector('a.google-button') as HTMLAnchorElement | null;
  const alert = () => element().querySelector('[role=alert]');

  it('signs in with Google through the API and returns to the requested page', async () => {
    fixture.componentRef.setInput('returnUrl', '/account');
    await fixture.whenStable();

    expect(link()?.getAttribute('href')).toBe('/api/account/sign-in?returnUrl=%2Faccount');
    expect(link()?.textContent?.trim()).toBe('Sign in with Google');
    expect(alert()).toBeNull();
  });

  it('returns to the home page by default', async () => {
    await fixture.whenStable();

    expect(link()?.getAttribute('href')).toBe('/api/account/sign-in?returnUrl=%2F');
  });

  it('never returns off the site', async () => {
    fixture.componentRef.setInput('returnUrl', '//evil.example.org');
    await fixture.whenStable();

    expect(link()?.getAttribute('href')).toBe('/api/account/sign-in?returnUrl=%2F');
  });

  it('says when sign-in failed', async () => {
    fixture.componentRef.setInput('failed', 'true');
    await fixture.whenStable();

    expect(alert()?.textContent).toContain('Sign-in did not complete');
  });

  it('says when Google sign-in is not set up on this instance', async () => {
    configuration.set({ google: { enabled: false } });
    await fixture.whenStable();

    expect(link()).toBeNull();
    expect(element().querySelector('[role=status]')?.textContent).toContain('not set up');
  });

  it('says when the server cannot be reached', async () => {
    configuration.set(null);
    await fixture.whenStable();

    expect(link()).toBeNull();
    expect(alert()?.textContent).toContain('could not be reached');
  });
});
