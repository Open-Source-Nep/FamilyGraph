import { HttpClient } from '@angular/common/http';
import { Service, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { Claim, ClaimList, ProfileClaimStatus } from './claim';

/** Identity claim requests (PRD 04). The server checks who may claim, decide or withdraw. */
@Service()
export class ClaimsService {
  private readonly http = inject(HttpClient);

  forPerson(personId: string): Promise<ProfileClaimStatus> {
    return firstValueFrom(this.http.get<ProfileClaimStatus>(`${this.personUrl(personId)}/claims`));
  }

  submit(personId: string, evidence: string | null): Promise<Claim> {
    return firstValueFrom(
      this.http.post<Claim>(`${this.personUrl(personId)}/claims`, { evidence }),
    );
  }

  /** "This is me": marks a profile you maintain as yourself. */
  claimAsSelf(personId: string): Promise<void> {
    return firstValueFrom(this.http.post<void>(`${this.personUrl(personId)}/claim-as-self`, null));
  }

  toDecide(): Promise<ClaimList> {
    return firstValueFrom(this.http.get<ClaimList>('/api/claims/to-decide'));
  }

  mine(): Promise<ClaimList> {
    return firstValueFrom(this.http.get<ClaimList>('/api/claims/mine'));
  }

  approve(claimId: string): Promise<void> {
    return this.act(claimId, 'approve');
  }

  reject(claimId: string): Promise<void> {
    return this.act(claimId, 'reject');
  }

  withdraw(claimId: string): Promise<void> {
    return this.act(claimId, 'withdraw');
  }

  private act(claimId: string, action: string): Promise<void> {
    return firstValueFrom(
      this.http.post<void>(`/api/claims/${encodeURIComponent(claimId)}/${action}`, null),
    );
  }

  private personUrl(personId: string): string {
    return `/api/people/${encodeURIComponent(personId)}`;
  }
}
