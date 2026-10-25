import { HttpClient } from '@angular/common/http';
import { Service, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { BirthDate } from './birth-date';
import {
  Gender,
  Person,
  PersonDetails,
  PeoplePage,
  PersonName,
  PersonSearchResult,
  ProfileList,
  Visibility,
} from './person';

/** Person profile requests. The server checks who may change a profile. */
@Service()
export class PeopleService {
  private readonly http = inject(HttpClient);

  /** Only the first name is required; the other details are optional (D-030, D-037). */
  create({ name, ...details }: PersonDetails): Promise<Person> {
    return firstValueFrom(this.http.post<Person>('/api/people', { ...name, ...details }));
  }

  get(id: string): Promise<Person> {
    return firstValueFrom(this.http.get<Person>(`/api/people/${encodeURIComponent(id)}`));
  }

  rename(id: string, name: PersonName): Promise<void> {
    return firstValueFrom(this.http.put<void>(`/api/people/${encodeURIComponent(id)}/name`, name));
  }

  /** Sets, changes or clears (null) the gender; only the profile's maintainer may (D-030). */
  setGender(id: string, gender: Gender | null): Promise<void> {
    return firstValueFrom(
      this.http.put<void>(`/api/people/${encodeURIComponent(id)}/gender`, { gender }),
    );
  }

  /** Sets, changes or clears (null) the description; only the profile's maintainer may (D-037). */
  setDescription(id: string, description: string | null): Promise<void> {
    return firstValueFrom(
      this.http.put<void>(`/api/people/${encodeURIComponent(id)}/description`, { description }),
    );
  }

  /** Sets or clears the birth date and whether others see it; only the profile's maintainer may (D-037). */
  setBirthDate(id: string, birthDate: BirthDate | null, shown: boolean): Promise<void> {
    return firstValueFrom(
      this.http.put<void>(`/api/people/${encodeURIComponent(id)}/birth-date`, { birthDate, shown }),
    );
  }

  /** Only the profile's maintainer may change it (PRD 05). */
  setVisibility(id: string, visibility: Visibility): Promise<void> {
    return firstValueFrom(
      this.http.put<void>(`/api/people/${encodeURIComponent(id)}/visibility`, { visibility }),
    );
  }

  /** Stores an image the browser has already shrunk (`preparePhoto`); only the maintainer may (D-028). */
  async setImage(id: string, image: Blob): Promise<string> {
    const response = await firstValueFrom(
      this.http.put<{ imageId: string }>(`/api/people/${encodeURIComponent(id)}/image`, image, {
        headers: { 'Content-Type': image.type },
      }),
    );
    return response.imageId;
  }

  removeImage(id: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`/api/people/${encodeURIComponent(id)}/image`));
  }

  /** Erases the profile for good; its maintainer or an administrator may (PRD 05). */
  delete(id: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`/api/people/${encodeURIComponent(id)}`));
  }

  listMaintained(): Promise<ProfileList> {
    return firstValueFrom(this.http.get<ProfileList>('/api/people/maintained'));
  }

  /**
   * One page of everyone you may see by name, A to Z, from `offset` (D-042): Public profiles, your
   * own and, for administrators, Private ones. Words in `text` must each appear in a name part or
   * the description. `hasMore` says another page follows.
   */
  browse(text: string, offset: number): Promise<PeoplePage> {
    const q = text.trim();
    return firstValueFrom(
      this.http.get<PeoplePage>('/api/people', {
        params: q ? { q, offset } : { offset },
      }),
    );
  }

  /**
   * Public profiles and your own whose names contain every word of `text`, the top matches only.
   * A Private profile is never found by its hidden name (D-032).
   */
  search(text: string): Promise<PersonSearchResult> {
    return firstValueFrom(
      this.http.get<PersonSearchResult>('/api/people/search', { params: { q: text } }),
    );
  }
}
