import { HttpClient } from '@angular/common/http';
import { Service, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { Bansawali, BansawaliLine } from './bansawali';
import { Connections, GraphOverview, LocalView } from './graph';
import { currentCulture } from '../../core/i18n/culture';
import { currentLanguage } from '../../core/i18n/language';

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

  /** Everyone connected to your profiles, in one bounded response, for the home page. */
  overview(): Promise<GraphOverview> {
    return firstValueFrom(this.http.get<GraphOverview>('/api/graph/overview'));
  }

  /**
   * How two people are connected, with the other person's relationship label in the current
   * culture, written in the current language when the culture has it (D-048).
   */
  connections(personId: string, otherId: string): Promise<Connections> {
    const culture = currentCulture();
    return firstValueFrom(
      this.http.get<Connections>(
        `/api/people/${encodeURIComponent(personId)}/connections/${encodeURIComponent(otherId)}`,
        { params: culture ? { culture: culture.code, language: currentLanguage() } : {} },
      ),
    );
  }

  /** The person's Bansawali: their descendants through sons, or through every child (D-043). */
  bansawali(personId: string, line: BansawaliLine): Promise<Bansawali> {
    return firstValueFrom(
      this.http.get<Bansawali>(`/api/people/${encodeURIComponent(personId)}/bansawali`, {
        params: { line },
      }),
    );
  }
}
