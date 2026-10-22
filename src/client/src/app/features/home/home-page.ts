import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { forgetCentre, rememberedCentre } from '../graph/graph-centre';
import { GraphView } from '../graph/graph-view';
import { PeopleService } from '../people/people.service';
import { PersonSummary, fullName } from '../people/person';
import { problemMessage } from '../people/problem';

/**
 * The family graph around the person this browser last centred a graph on, or else your first
 * profile (D-024). An account is not a person, so you can switch to any profile you maintain.
 */
@Component({
  selector: 'app-home-page',
  imports: [GraphView, RouterLink],
  templateUrl: './home-page.html',
})
export class HomePage {
  private readonly people = inject(PeopleService);

  protected readonly account = inject(AuthService).account;
  protected readonly fullName = fullName;
  protected readonly profiles = signal<PersonSummary[] | null>(null);
  protected readonly centreId = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);
  /** The centre is one of your profiles, so the picker can show it. */
  protected readonly centreIsYours = computed(
    () => this.profiles()?.some((person) => person.id === this.centreId()) ?? false,
  );

  constructor() {
    this.people.listMaintained().then(
      (list) => {
        this.profiles.set(list.people);
        this.centreId.set(rememberedCentre() ?? list.people[0]?.id ?? null);
      },
      (error: unknown) => this.error.set(problemMessage(error)),
    );
  }

  protected centreOn(personId: string): void {
    this.centreId.set(personId || null);
  }

  /** The remembered person is gone or hidden: fall back to your first profile, once. */
  protected fallBack(): void {
    forgetCentre();
    const first = this.profiles()?.[0]?.id ?? null;
    this.centreId.set(first === this.centreId() ? null : first);
  }
}
