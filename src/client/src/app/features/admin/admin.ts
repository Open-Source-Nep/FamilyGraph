/** An account on the administrator's Users page, from `GET /api/admin/accounts` (D-035). */
export interface AccountSummary {
  id: string;
  email: string;
  emailVerified: boolean;
  administrator: boolean;
  maintainedProfiles: number;
}

/** `isTruncated` says the list stopped at the server's limit. */
export interface AccountList {
  accounts: AccountSummary[];
  isTruncated: boolean;
}

/** How many profiles moved to the other account. */
export interface TransferredProfiles {
  transferred: number;
}

/** The account's email, marked when Google has not verified it. */
export function accountLabel(account: AccountSummary): string {
  return account.emailVerified ? account.email : `${account.email} (not verified)`;
}
