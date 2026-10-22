import { HttpParams } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';

import { AuthService } from '../../core/auth/auth.service';
import { GoogleCallbackPage } from './google-callback-page';

describe('GoogleCallbackPage', () => {
  let completeGoogleSignIn: (googleResult: HttpParams) => Promise<string>;
  let received: HttpParams | undefined;

  beforeEach(() => {
    received = undefined;
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'google-callback', component: GoogleCallbackPage },
          { path: '**', children: [] },
        ]),
        {
          provide: AuthService,
          useValue: {
            completeGoogleSignIn: (googleResult: HttpParams) => {
              received = googleResult;
              return completeGoogleSignIn(googleResult);
            },
          },
        },
      ],
    });
  });

  it("passes Google's result to the API and goes to the returned page", async () => {
    completeGoogleSignIn = () => Promise.resolve('/people');
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/google-callback?code=c&state=s');
    await harness.fixture.whenStable();

    expect(received?.toString()).toBe('code=c&state=s');
    expect(TestBed.inject(Router).url).toBe('/people');
  });

  it('goes back to the sign-in page when sign-in fails', async () => {
    completeGoogleSignIn = () => Promise.reject(new Error('401'));
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/google-callback?error=access_denied&state=s');
    await harness.fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe('/sign-in?failed=true');
  });
});
