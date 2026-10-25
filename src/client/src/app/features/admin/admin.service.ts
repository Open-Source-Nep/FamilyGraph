import { HttpClient } from '@angular/common/http';
import { Service, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { ProfileList } from '../people/person';
import { AccountList, TransferredProfiles } from './admin';

/**
 * The administrator's Users page and profile transfers (D-035). The server refuses everyone who is
 * not an administrator.
 */
@Service()
export class AdminService {
  private readonly http = inject(HttpClient);

  listAccounts(): Promise<AccountList> {
    return firstValueFrom(this.http.get<AccountList>('/api/admin/accounts'));
  }

  /** The non-deleted profiles the account maintains, Private ones with their names. */
  listProfiles(accountId: string): Promise<ProfileList> {
    return firstValueFrom(
      this.http.get<ProfileList>(`/api/admin/accounts/${encodeURIComponent(accountId)}/profiles`),
    );
  }

  /** Makes another account the profile's maintainer, without its approval. */
  transferProfile(personId: string, toAccountId: string): Promise<void> {
    return firstValueFrom(
      this.http.post<void>(`/api/admin/people/${encodeURIComponent(personId)}/transfer`, {
        toAccountId,
      }),
    );
  }

  /** Moves every profile the account maintains to another account. */
  transferAll(fromAccountId: string, toAccountId: string): Promise<TransferredProfiles> {
    return firstValueFrom(
      this.http.post<TransferredProfiles>(
        `/api/admin/accounts/${encodeURIComponent(fromAccountId)}/transfer`,
        { toAccountId },
      ),
    );
  }
}
