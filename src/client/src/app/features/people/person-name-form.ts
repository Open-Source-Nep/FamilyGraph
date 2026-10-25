import { Component, effect, input, output, signal, viewChild } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';

import { BirthDate } from './birth-date';
import { BirthDateInput } from './birth-date-input';
import {
  Gender,
  PersonDetails,
  PersonName,
  genders,
  maxDescriptionLength,
  maxNamePartLength,
} from './person';

/**
 * First, middle and last name fields, the gender, a one-line description and the birth date with
 * its "show to others" switch, off by default. Only the first name is required (PRD 01); the rest
 * may stay empty (D-030, D-037).
 */
@Component({
  selector: 'app-person-name-form',
  imports: [BirthDateInput, ReactiveFormsModule],
  templateUrl: './person-name-form.html',
})
export class PersonNameForm {
  readonly name = input<PersonName | null>(null);
  readonly gender = input<Gender | null>(null);
  readonly description = input<string | null>(null);
  readonly birthDate = input<BirthDate | null>(null);
  readonly birthDateShown = input(false);
  readonly submitLabel = input('Save');
  readonly busy = input(false);
  readonly saved = output<PersonDetails>();
  readonly cancelled = output<void>();

  protected readonly maxLength = maxNamePartLength;
  protected readonly genders = genders;
  protected readonly maxDescriptionLength = maxDescriptionLength;
  protected readonly date = signal<BirthDate | null>(null);
  private readonly dateInput = viewChild(BirthDateInput);
  protected readonly form = new FormGroup({
    firstName: new FormControl('', {
      nonNullable: true,
      validators: [
        Validators.required,
        Validators.pattern(/\S/),
        Validators.maxLength(maxNamePartLength),
      ],
    }),
    middleName: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(maxNamePartLength)],
    }),
    lastName: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(maxNamePartLength)],
    }),
    gender: new FormControl<Gender | null>(null),
    description: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(maxDescriptionLength)],
    }),
    birthDateShown: new FormControl(false, { nonNullable: true }),
  });

  constructor() {
    effect(() => {
      const name = this.name();
      this.form.reset({
        firstName: name?.firstName ?? '',
        middleName: name?.middleName ?? '',
        lastName: name?.lastName ?? '',
        gender: this.gender(),
        description: this.description() ?? '',
        birthDateShown: this.birthDateShown(),
      });
      this.date.set(this.birthDate());
    });
  }

  protected submit(): void {
    if (this.form.invalid || this.dateInput()?.invalid()) {
      this.form.markAllAsTouched();
      return;
    }
    const { firstName, middleName, lastName, gender, description, birthDateShown } =
      this.form.getRawValue();
    const birthDate = this.date();
    this.saved.emit({
      name: {
        firstName: firstName.trim(),
        middleName: middleName.trim() || null,
        lastName: lastName.trim() || null,
      },
      gender,
      description: description.replace(/\s+/g, ' ').trim() || null,
      birthDate,
      birthDateShown: birthDate !== null && birthDateShown,
    });
  }
}
