/** The signed-in user's account, as returned by `GET /api/account`. It is not a person profile. */
export interface Account {
  id: string;
  email: string;
  emailVerified: boolean;
  roles: string[];
}

/** Where the browser goes to sign in with Google. The first sign-in registers the account. */
export function signInUrl(returnUrl: string): string {
  return `/api/account/sign-in?returnUrl=${encodeURIComponent(returnUrl)}`;
}
