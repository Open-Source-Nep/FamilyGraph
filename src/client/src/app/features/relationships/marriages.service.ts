import { HttpClient } from '@angular/common/http';
import { Service, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import {
  CreatedMarriage,
  MarriageConsent,
  MarriageConsentList,
  NewMarriage,
  SpouseList,
} from './relationship';

/**
 * Marriage requests (D-031). The server checks who may create, approve, mark or delete a marriage,
 * and rejects a marriage between a parent and their child.
 */
@Service()
export class MarriagesService {
  private readonly http = inject(HttpClient);

  create(marriage: NewMarriage): Promise<CreatedMarriage> {
    return firstValueFrom(this.http.post<CreatedMarriage>('/api/marriages', marriage));
  }

  listSpouses(personId: string): Promise<SpouseList> {
    return firstValueFrom(
      this.http.get<SpouseList>(`/api/people/${encodeURIComponent(personId)}/spouses`),
    );
  }

  /** The inactive marriages with a profile you maintain, those waiting for you first. */
  listInactive(): Promise<MarriageConsentList> {
    return firstValueFrom(this.http.get<MarriageConsentList>('/api/marriages/inactive'));
  }

  /** Approves for a spouse you maintain, from pending or after withdrawing. */
  approve(marriageId: string, spouseId: string): Promise<MarriageConsent> {
    return this.consent(marriageId, 'approve', spouseId);
  }

  /** Declines for a pending spouse you maintain, or withdraws your approval. */
  disagree(marriageId: string, spouseId: string): Promise<MarriageConsent> {
    return this.consent(marriageId, 'disagree', spouseId);
  }

  /** Marks the marriage former (ended) or current again. Needs no consent. */
  setFormer(marriageId: string, isFormer: boolean): Promise<void> {
    return firstValueFrom(
      this.http.put<void>(`/api/marriages/${encodeURIComponent(marriageId)}/former`, { isFormer }),
    );
  }

  /** Deletes a marriage whose two spouses you maintain. Deletion is final. */
  delete(marriageId: string): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>(`/api/marriages/${encodeURIComponent(marriageId)}`),
    );
  }

  private consent(
    marriageId: string,
    action: 'approve' | 'disagree',
    spouseId: string,
  ): Promise<MarriageConsent> {
    return firstValueFrom(
      this.http.post<MarriageConsent>(
        `/api/marriages/${encodeURIComponent(marriageId)}/${action}`,
        { spouseId },
      ),
    );
  }
}
