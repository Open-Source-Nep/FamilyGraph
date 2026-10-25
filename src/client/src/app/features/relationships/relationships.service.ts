import { HttpClient } from '@angular/common/http';
import { Service, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import {
  CreatedLink,
  DirectRelatives,
  LinkConsent,
  LinkConsentList,
  LinkEndpoint,
  NewLink,
} from './relationship';

/**
 * Parent–child link requests. The server checks who may create, approve or delete a link, and
 * rejects cycles.
 */
@Service()
export class RelationshipsService {
  private readonly http = inject(HttpClient);

  create(link: NewLink): Promise<CreatedLink> {
    return firstValueFrom(this.http.post<CreatedLink>('/api/relationships', link));
  }

  listDirectRelatives(personId: string): Promise<DirectRelatives> {
    return firstValueFrom(
      this.http.get<DirectRelatives>(`/api/people/${encodeURIComponent(personId)}/relatives`),
    );
  }

  /** The inactive links with a profile you maintain, those waiting for you first. */
  listInactive(): Promise<LinkConsentList> {
    return firstValueFrom(this.http.get<LinkConsentList>('/api/relationships/inactive'));
  }

  /** Every link with a side waiting for another maintainer; administrators only (D-040). */
  listPending(): Promise<LinkConsentList> {
    return firstValueFrom(this.http.get<LinkConsentList>('/api/relationships/pending'));
  }

  /** An administrator approves a pending endpoint on behalf of its maintainer (D-040). */
  approveAsAdministrator(linkId: string, endpoint: LinkEndpoint): Promise<LinkConsent> {
    return firstValueFrom(
      this.http.post<LinkConsent>(
        `/api/relationships/${encodeURIComponent(linkId)}/approve-as-administrator`,
        { endpoint },
      ),
    );
  }

  /** Approves a pending endpoint you maintain, or restores your approval. */
  approve(linkId: string, endpoint: LinkEndpoint): Promise<LinkConsent> {
    return this.consent(linkId, 'approve', endpoint);
  }

  /** Declines a pending endpoint you maintain, or withdraws your approval. */
  disagree(linkId: string, endpoint: LinkEndpoint): Promise<LinkConsent> {
    return this.consent(linkId, 'disagree', endpoint);
  }

  /**
   * Numbers the parent's children 1, 2, 3… in the order of `linkIds`, eldest first; only the
   * parent's maintainer may (D-016, D-037).
   */
  orderChildren(parentId: string, linkIds: readonly string[]): Promise<void> {
    return firstValueFrom(
      this.http.put<void>(`/api/people/${encodeURIComponent(parentId)}/children/order`, {
        linkIds,
      }),
    );
  }

  /** Deletes a link whose two people you maintain. Deletion is final (D-014). */
  delete(linkId: string): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>(`/api/relationships/${encodeURIComponent(linkId)}`),
    );
  }

  private consent(
    linkId: string,
    action: 'approve' | 'disagree',
    endpoint: LinkEndpoint,
  ): Promise<LinkConsent> {
    return firstValueFrom(
      this.http.post<LinkConsent>(`/api/relationships/${encodeURIComponent(linkId)}/${action}`, {
        endpoint,
      }),
    );
  }
}
