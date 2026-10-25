import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { PeopleService } from './people.service';
import { PersonSummary, ProfileList, fullName } from './person';
import { PersonAvatar } from './person-avatar';
import { problemMessage } from './problem';

/**
 * The profiles the signed-in user maintains (PRD 01: one account, many profiles), each a row with
 * its photo or initials and its description (D-038). The search box filters the loaded list as you
 * type: every word must appear in a name part or the description, ignoring case.
 */
@Component({
  selector: 'app-people-page',
  imports: [PersonAvatar, RouterLink],
  templateUrl: './people-page.html',
})
export class PeoplePage {
  private readonly people = inject(PeopleService);

  protected readonly fullName = fullName;
  protected readonly list = signal<ProfileList | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly query = signal('');
  protected readonly shown = computed(() => {
    const people = this.list()?.people ?? [];
    const words = this.query().toLocaleLowerCase().split(/\s+/).filter(Boolean);
    return words.length === 0 ? people : people.filter((person) => matches(person, words));
  });

  constructor() {
    this.people.listMaintained().then(
      (list) => this.list.set(list),
      (error: unknown) => this.error.set(problemMessage(error)),
    );
  }
}

/** Every word appears in the first, middle or last name or the description. */
function matches(person: PersonSummary, words: string[]): boolean {
  const parts = [person.firstName, person.middleName, person.lastName, person.description]
    .filter((part): part is string => Boolean(part))
    .map((part) => part.toLocaleLowerCase());
  return words.every((word) => parts.some((part) => part.includes(word)));
}
