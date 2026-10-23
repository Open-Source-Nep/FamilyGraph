import { HttpClient } from '@angular/common/http';
import { Service, inject } from '@angular/core';
import { firstValueFrom, map } from 'rxjs';

import { AcceptedHandover, CreatedHandover, HandoverPreview, OpenHandover } from './handover';

/** Profile handover requests (PRD 01). The server checks who may create, see, cancel or accept a link. */
@Service()
export class HandoversService {
  private readonly http = inject(HttpClient);

  /** Creates a handover link, replacing the profile's open one. Maintainer only. */
  offer(personId: string): Promise<CreatedHandover> {
    return firstValueFrom(this.http.post<CreatedHandover>(this.url(personId), null));
  }

  /** The profile's open link, or `null` when there is none. Maintainer only. */
  getOpen(personId: string): Promise<OpenHandover | null> {
    return firstValueFrom(
      this.http
        .get<OpenHandover | null>(this.url(personId), { observe: 'response' })
        .pipe(map((response) => (response.status === 204 ? null : response.body))),
    );
  }

  cancel(personId: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(this.url(personId)));
  }

  preview(token: string): Promise<HandoverPreview> {
    return firstValueFrom(this.http.post<HandoverPreview>('/api/handovers/preview', { token }));
  }

  accept(token: string): Promise<AcceptedHandover> {
    return firstValueFrom(this.http.post<AcceptedHandover>('/api/handovers/accept', { token }));
  }

  private url(personId: string): string {
    return `/api/people/${encodeURIComponent(personId)}/handover`;
  }
}
