/** The signed-in user's account, as returned by `GET /api/account`. It is not a person profile. */
export interface Account {
  id: string;
  email: string;
  emailVerified: boolean;
  roles: string[];
}

/** Keeps a return URL on this site; anything else becomes the home page. The server checks too. */
export function localReturnUrl(returnUrl: string | null | undefined): string {
  return returnUrl && /^\/(?![/\\])/.test(returnUrl) ? returnUrl : '/';
}
