import { HttpClient } from '@angular/common/http';
import { Service, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { Connections, LocalView } from './graph';

/** Graph exploration requests. The server bounds every query and walks only active links. */
@Service()
export class GraphService {
  private readonly http = inject(HttpClient);

  /** The person and the people nearest to them. "Load more" is this view centred on an edge person. */
  localView(personId: string): Promise<LocalView> {
    return firstValueFrom(
      this.http.get<LocalView>(`/api/people/${encodeURIComponent(personId)}/graph`),
    );
  }

  connections(personId: string, otherId: string): Promise<Connections> {
    return firstValueFrom(
      this.http.get<Connections>(
        `/api/people/${encodeURIComponent(personId)}/connections/${encodeURIComponent(otherId)}`,
      ),
    );
  }
}
