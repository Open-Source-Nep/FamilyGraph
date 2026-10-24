import { Component, effect, input, output } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';

import { Gender, PersonDetails, PersonName, genders, maxNamePartLength } from './person';

/**
 * First, middle and last name fields and the gender. Only the first name is required (PRD 01);
 * the gender may stay "Not set" (D-030).
 */
@Component({
  selector: 'app-person-name-form',
  imports: [ReactiveFormsModule],
  templateUrl: './person-name-form.html',
})
export class PersonNameForm {
  readonly name = input<PersonName | null>(null);
  readonly gender = input<Gender | null>(null);
  readonly submitLabel = input('Save');
  readonly busy = input(false);
  readonly saved = output<PersonDetails>();
  readonly cancelled = output<void>();

  protected readonly maxLength = maxNamePartLength;
  protected readonly genders = genders;
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
  });

  constructor() {
    effect(() => {
      const name = this.name();
      this.form.reset({
        firstName: name?.firstName ?? '',
        middleName: name?.middleName ?? '',
        lastName: name?.lastName ?? '',
        gender: this.gender(),
      });
    });
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const { firstName, middleName, lastName, gender } = this.form.getRawValue();
    this.saved.emit({
      name: {
        firstName: firstName.trim(),
        middleName: middleName.trim() || null,
        lastName: lastName.trim() || null,
      },
      gender,
    });
  }
}
