import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { PersonRelatives } from '../relationships/person-relatives';
import { PeopleService } from './people.service';
import { Person, PersonName, Visibility, fullName } from './person';
import { PersonNameForm } from './person-name-form';
import { problemMessage } from './problem';

/**
 * One profile with its parents and children. Its maintainer can rename it and make it Public or
 * Private; the server enforces both (PRD 01, PRD 05). A profile hidden from you shows only the
 * `Private` placeholder, with its parents and children.
 */
@Component({
  selector: 'app-person-page',
  imports: [PersonNameForm, PersonRelatives, RouterLink],
  templateUrl: './person-page.html',
})
export class PersonPage {
  private readonly people = inject(PeopleService);

  /** The `:id` route parameter. */
  readonly id = input.required<string>();

  protected readonly person = signal<Person | null>(null);
  protected readonly name = computed(() => {
    const person = this.person();
    return person ? fullName(person) : '';
  });
  /** Only the maintainer edits, and they always see the name. */
  protected readonly editableName = computed<PersonName | null>(() => {
    const person = this.person();
    return person?.firstName != null
      ? { firstName: person.firstName, middleName: person.middleName, lastName: person.lastName }
      : null;
  });
  protected readonly editing = signal(false);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  constructor() {
    effect(() => {
      const id = this.id();
      this.person.set(null);
      this.editing.set(false);
      this.error.set(null);
      this.people.get(id).then(
        (person) => this.person.set(person),
        (error: unknown) => this.error.set(problemMessage(error)),
      );
    });
  }

  protected async setVisibility(visibility: Visibility): Promise<void> {
    const person = this.person();
    if (!person) {
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    try {
      await this.people.setVisibility(person.id, visibility);
      this.person.set({ ...person, visibility });
    } catch (error) {
      this.error.set(problemMessage(error));
    } finally {
      this.saving.set(false);
    }
  }

  protected async rename(name: PersonName): Promise<void> {
    const person = this.person();
    if (!person) {
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    try {
      await this.people.rename(person.id, name);
      this.person.set({ ...person, ...name });
      this.editing.set(false);
    } catch (error) {
      this.error.set(problemMessage(error));
    } finally {
      this.saving.set(false);
    }
  }
}
