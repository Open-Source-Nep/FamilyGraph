import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';

import { PersonTransfer } from '../admin/person-transfer';
import { PersonHandover } from '../handovers/person-handover';
import { PersonRelatives } from '../relationships/person-relatives';
import { PersonSpouses } from '../relationships/person-spouses';
import { PeopleService } from './people.service';
import { BirthDate, formatBirthDate } from './birth-date';
import { Person, PersonDetails, PersonName, Visibility, fullName } from './person';
import { PersonNameForm } from './person-name-form';
import { PersonPhoto } from './person-photo';
import { problemMessage } from './problem';

/**
 * One profile with its photo, parents and children. Its maintainer can rename it, change its photo,
 * make it Public or Private and hand it over to another account, and its maintainer or an
 * administrator can delete it. An administrator can also give it to another account (D-035). The
 * server enforces all of these (PRD 01, PRD 05). A Private profile hidden from you or a deleted one shows only its
 * placeholder, with its parents and children.
 */
@Component({
  selector: 'app-person-page',
  imports: [
    PersonHandover,
    PersonNameForm,
    PersonPhoto,
    PersonRelatives,
    PersonSpouses,
    PersonTransfer,
    RouterLink,
  ],
  templateUrl: './person-page.html',
})
export class PersonPage {
  private readonly people = inject(PeopleService);
  private readonly auth = inject(AuthService);

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
  protected readonly canDelete = computed(() => {
    const person = this.person();
    return (
      !!person &&
      person.placeholder !== 'Deleted' &&
      (person.maintainedByYou || this.auth.isAdministrator())
    );
  });
  protected readonly isAdministrator = this.auth.isAdministrator;
  protected readonly birthDate = computed(() => {
    const date = this.person()?.birthDate;
    return date ? formatBirthDate(date) : null;
  });
  protected readonly confirmingDelete = signal(false);
  protected readonly editing = signal(false);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  constructor() {
    effect(() => {
      const id = this.id();
      this.person.set(null);
      this.editing.set(false);
      this.confirmingDelete.set(false);
      this.error.set(null);
      this.people.get(id).then(
        (person) => this.person.set(person),
        (error: unknown) => this.error.set(problemMessage(error)),
      );
    });
  }

  protected photoChanged(imageId: string | null): void {
    const person = this.person();
    if (person) {
      this.person.set({ ...person, imageId });
    }
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

  /** Deletion cannot be undone; the profile stays as a `Deleted` placeholder with its links. */
  protected async deleteProfile(): Promise<void> {
    const person = this.person();
    if (!person) {
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    try {
      await this.people.delete(person.id);
      this.confirmingDelete.set(false);
      this.person.set(await this.people.get(person.id));
    } catch (error) {
      this.error.set(problemMessage(error));
    } finally {
      this.saving.set(false);
    }
  }

  /** After an administrator's transfer, `maintainedByYou` may have changed. */
  protected async transferred(): Promise<void> {
    const person = this.person();
    if (!person) {
      return;
    }
    try {
      this.person.set(await this.people.get(person.id));
    } catch (error) {
      this.error.set(problemMessage(error));
    }
  }

  /** Saves the name, gender, description and birth date, each only when it changed. */
  protected async saveDetails({
    name,
    gender,
    description,
    birthDate,
    birthDateShown,
  }: PersonDetails): Promise<void> {
    const person = this.person();
    if (!person) {
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    try {
      const previous = this.editableName();
      if (
        name.firstName !== previous?.firstName ||
        name.middleName !== previous.middleName ||
        name.lastName !== previous.lastName
      ) {
        await this.people.rename(person.id, name);
        this.person.set({ ...person, ...name });
      }
      if (gender !== person.gender) {
        await this.people.setGender(person.id, gender);
        this.person.update((current) => current && { ...current, gender });
      }
      if (description !== person.description) {
        await this.people.setDescription(person.id, description);
        this.person.update((current) => current && { ...current, description });
      }
      if (!sameDate(birthDate, person.birthDate) || birthDateShown !== person.birthDateShown) {
        await this.people.setBirthDate(person.id, birthDate, birthDateShown);
        this.person.update((current) => current && { ...current, birthDate, birthDateShown });
      }
      this.editing.set(false);
    } catch (error) {
      this.error.set(problemMessage(error));
    } finally {
      this.saving.set(false);
    }
  }
}

function sameDate(a: BirthDate | null, b: BirthDate | null): boolean {
  return (
    a === b ||
    (!!a &&
      !!b &&
      a.calendar === b.calendar &&
      a.year === b.year &&
      a.month === b.month &&
      a.day === b.day)
  );
}
