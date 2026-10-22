import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { GoogleSignInService } from '../../core/auth/google-sign-in.service';
import { ClientConfiguration } from '../../core/config/client-configuration';
import { ConfigurationService } from '../../core/config/configuration.service';
import { SignInPage } from './sign-in-page';

describe('SignInPage', () => {
  const configuration = signal<ClientConfiguration | null>(null);
  let fixture: ComponentFixture<SignInPage>;
  let started: [string, string] | undefined;

  beforeEach(() => {
    configuration.set({ google: { enabled: true, clientId: 'client-id' } });
    started = undefined;
    TestBed.configureTestingModule({
      providers: [
        { provide: ConfigurationService, useValue: { configuration } },
        {
          provide: GoogleSignInService,
          useValue: {
            start: (clientId: string, returnUrl: string) => {
              started = [clientId, returnUrl];
              return Promise.resolve();
            },
          },
        },
      ],
    });
    fixture = TestBed.createComponent(SignInPage);
  });

  const element = () => fixture.nativeElement as HTMLElement;
  const button = () => element().querySelector('button.google-button') as HTMLButtonElement | null;
  const alert = () => element().querySelector('[role=alert]');

  it('starts Google sign-in with the client ID and returns to the requested page', async () => {
    fixture.componentRef.setInput('returnUrl', '/account');
    await fixture.whenStable();

    button()!.click();

    expect(button()?.textContent?.trim()).toBe('Sign in with Google');
    expect(started).toEqual(['client-id', '/account']);
    expect(alert()).toBeNull();
  });

  it('returns to the home page by default', async () => {
    await fixture.whenStable();

    button()!.click();

    expect(started).toEqual(['client-id', '/']);
  });

  it('says when sign-in failed', async () => {
    fixture.componentRef.setInput('failed', 'true');
    await fixture.whenStable();

    expect(alert()?.textContent).toContain('Sign-in did not complete');
  });

  it('says when Google sign-in is not set up on this instance', async () => {
    configuration.set({ google: { enabled: false, clientId: null } });
    await fixture.whenStable();

    expect(button()).toBeNull();
    expect(element().querySelector('[role=status]')?.textContent).toContain('not set up');
  });

  it('says when the server cannot be reached', async () => {
    configuration.set(null);
    await fixture.whenStable();

    expect(button()).toBeNull();
    expect(alert()?.textContent).toContain('could not be reached');
  });
});
