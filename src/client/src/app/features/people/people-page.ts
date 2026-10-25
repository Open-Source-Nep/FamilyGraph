import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { PeopleService } from './people.service';
import { ProfileList, fullName } from './person';
import { PersonAvatar } from './person-avatar';
import { problemMessage } from './problem';

/**
 * The profiles the signed-in user maintains (PRD 01: one account, many profiles), each a row with
 * its photo or initials and its description (D-038).
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

  constructor() {
    this.people.listMaintained().then(
      (list) => this.list.set(list),
      (error: unknown) => this.error.set(problemMessage(error)),
    );
  }
}
