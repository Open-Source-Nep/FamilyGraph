import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';

import { AuthService } from '../../core/auth/auth.service';
import { GoogleSignInResult, GoogleSignInService } from '../../core/auth/google-sign-in.service';
import { GoogleCallbackPage } from './google-callback-page';

describe('GoogleCallbackPage', () => {
  let googleResult: GoogleSignInResult | null;
  let completeGoogleSignIn: () => Promise<void>;
  let fragment: string | null | undefined;
  let sent: [string, string] | undefined;

  beforeEach(() => {
    fragment = undefined;
    sent = undefined;
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'google-callback', component: GoogleCallbackPage },
          { path: '**', children: [] },
        ]),
        {
          provide: GoogleSignInService,
          useValue: {
            complete: (value: string | null) => {
              fragment = value;
              return googleResult;
            },
          },
        },
        {
          provide: AuthService,
          useValue: {
            completeGoogleSignIn: (idToken: string, nonceSecret: string) => {
              sent = [idToken, nonceSecret];
              return completeGoogleSignIn();
            },
          },
        },
      ],
    });
  });

  it('sends the ID token to the API and goes to the page sign-in started from', async () => {
    googleResult = { idToken: 'token', nonceSecret: 'secret', returnUrl: '/people' };
    completeGoogleSignIn = () => Promise.resolve();
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/google-callback#id_token=token&state=s');
    await harness.fixture.whenStable();

    expect(fragment).toBe('id_token=token&state=s');
    expect(sent).toEqual(['token', 'secret']);
    expect(TestBed.inject(Router).url).toBe('/people');
  });

  it("goes back to the sign-in page when Google's answer is not accepted", async () => {
    googleResult = null;
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/google-callback#error=access_denied&state=s');
    await harness.fixture.whenStable();

    expect(sent).toBeUndefined();
    expect(TestBed.inject(Router).url).toBe('/sign-in?failed=true');
  });

  it('goes back to the sign-in page when the server rejects the token', async () => {
    googleResult = { idToken: 'token', nonceSecret: 'secret', returnUrl: '/people' };
    completeGoogleSignIn = () => Promise.reject(new Error('401'));
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/google-callback#id_token=token&state=s');
    await harness.fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe('/sign-in?failed=true');
  });
});
