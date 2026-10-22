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
 * Parent–child link requests. The server checks who may create a link or change an endpoint's
 * approval, and rejects cycles.
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

  /** Approves a pending endpoint you maintain, or restores your approval. */
  approve(linkId: string, endpoint: LinkEndpoint): Promise<LinkConsent> {
    return this.consent(linkId, 'approve', endpoint);
  }

  /** Declines a pending endpoint you maintain, or withdraws your approval. */
  disagree(linkId: string, endpoint: LinkEndpoint): Promise<LinkConsent> {
    return this.consent(linkId, 'disagree', endpoint);
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
