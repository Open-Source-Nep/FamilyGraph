import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Service, computed, inject, signal } from '@angular/core';
import { catchError, firstValueFrom, of, throwError } from 'rxjs';

import { Account } from './account';

/**
 * Knows who is signed in. The server decides every permission; this only shapes the UI.
 * Requests send the session cookie, and HttpClient adds the X-XSRF-TOKEN header to
 * state-changing requests from the XSRF-TOKEN cookie.
 */
@Service()
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly current = signal<Account | null>(null);
  private loading: Promise<Account | null> | null = null;

  readonly account = this.current.asReadonly();
  readonly isAdministrator = computed(
    () => this.current()?.roles.includes('Administrator') ?? false,
  );

  /** Loads the account once; `null` means nobody is signed in. */
  load(): Promise<Account | null> {
    this.loading ??= firstValueFrom(
      this.http
        .get<Account>('/api/account')
        .pipe(
          catchError((error: HttpErrorResponse) =>
            error.status === 401 ? of(null) : throwError(() => error),
          ),
        ),
    ).then(
      (account) => {
        this.current.set(account);
        return account;
      },
      (error: unknown) => {
        this.loading = null;
        throw error;
      },
    );
    return this.loading;
  }

  /**
   * Sends the ID token Google returned to `/google-callback` to the server, which checks it,
   * registers or updates the account and starts the session.
   */
  async completeGoogleSignIn(idToken: string, nonceSecret: string): Promise<void> {
    // Loading the account (signed out) issues the XSRF-TOKEN cookie the POST needs.
    this.loading = null;
    await this.load();
    await firstValueFrom(
      this.http.post<void>('/api/account/google-sign-in', { idToken, nonceSecret }),
    );
    // Reloading also issues the XSRF-TOKEN cookie for the new session.
    this.loading = null;
    await this.load();
  }

  async signOut(): Promise<void> {
    await firstValueFrom(this.http.post<void>('/api/account/sign-out', null));
    this.current.set(null);
    this.loading = Promise.resolve(null);
  }
}
