import { HttpClient } from '@angular/common/http';
import { Service, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { CreatedLink, DirectRelatives, NewLink } from './relationship';

/** Parent–child link requests. The server checks who may create a link and rejects cycles. */
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
}
