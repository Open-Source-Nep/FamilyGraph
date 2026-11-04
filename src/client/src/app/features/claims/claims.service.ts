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

  toRespond(): Promise<ClaimList> {
    return firstValueFrom(this.http.get<ClaimList>('/api/claims/to-respond'));
  }

  /** Asks administrators to review your claim: an appeal, or a claim the maintainer did not answer. */
  requestReview(claimId: string): Promise<void> {
    return this.act(claimId, 'review');
  }

  changeEvidence(claimId: string, evidence: string | null): Promise<void> {
    return firstValueFrom(this.http.put<void>(`${this.claimUrl(claimId)}/evidence`, { evidence }));
  }

  /** Your reply, as the claimed person, to a claim on your profile. */
  respond(claimId: string, response: string | null): Promise<void> {
    return firstValueFrom(this.http.put<void>(`${this.claimUrl(claimId)}/response`, { response }));
  }

  /** The claims waiting for administrators (D-052). */
  forAdministrators(): Promise<ClaimList> {
    return firstValueFrom(this.http.get<ClaimList>('/api/admin/claims'));
  }

  approveAsAdministrator(claimId: string, rationale: string): Promise<void> {
    return this.administer(claimId, 'approve', { rationale });
  }

  rejectAsAdministrator(claimId: string, rationale: string): Promise<void> {
    return this.administer(claimId, 'reject', { rationale });
  }

  askForEvidence(claimId: string, question: string): Promise<void> {
    return this.administer(claimId, 'ask', { question });
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
    return firstValueFrom(this.http.post<void>(`${this.claimUrl(claimId)}/${action}`, null));
  }

  private administer(claimId: string, action: string, body: object): Promise<void> {
    return firstValueFrom(
      this.http.post<void>(`/api/admin/claims/${encodeURIComponent(claimId)}/${action}`, body),
    );
  }

  private claimUrl(claimId: string): string {
    return `/api/claims/${encodeURIComponent(claimId)}`;
  }

  private personUrl(personId: string): string {
    return `/api/people/${encodeURIComponent(personId)}`;
  }
}
