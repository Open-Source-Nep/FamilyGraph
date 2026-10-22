import { HttpClient } from '@angular/common/http';
import { Service, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { Person, PersonName, ProfileList } from './person';

/** Person profile requests. The server checks who may change a profile. */
@Service()
export class PeopleService {
  private readonly http = inject(HttpClient);

  create(name: PersonName): Promise<Person> {
    return firstValueFrom(this.http.post<Person>('/api/people', name));
  }

  get(id: string): Promise<Person> {
    return firstValueFrom(this.http.get<Person>(`/api/people/${encodeURIComponent(id)}`));
  }

  rename(id: string, name: PersonName): Promise<void> {
    return firstValueFrom(this.http.put<void>(`/api/people/${encodeURIComponent(id)}/name`, name));
  }

  listMaintained(): Promise<ProfileList> {
    return firstValueFrom(this.http.get<ProfileList>('/api/people/maintained'));
  }
}
