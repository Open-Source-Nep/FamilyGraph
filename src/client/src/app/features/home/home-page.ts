import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { HomeGraph } from '../graph/home-graph';
import { PeopleService } from '../people/people.service';
import { PersonSummary } from '../people/person';
import { problemMessage } from '../people/problem';

/**
 * The family graph of everyone connected to the profiles you maintain, as dots and lines (D-024).
 * An account is not a person, so nobody is the centre; selecting someone opens their graph page.
 */
@Component({
  selector: 'app-home-page',
  imports: [HomeGraph, RouterLink],
  templateUrl: './home-page.html',
})
export class HomePage {
  private readonly people = inject(PeopleService);

  protected readonly account = inject(AuthService).account;
  protected readonly profiles = signal<PersonSummary[] | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly profileIds = computed(() => this.profiles()?.map((person) => person.id) ?? []);

  constructor() {
    this.people.listMaintained().then(
      (list) => this.profiles.set(list.people),
      (error: unknown) => this.error.set(problemMessage(error)),
    );
  }
}
