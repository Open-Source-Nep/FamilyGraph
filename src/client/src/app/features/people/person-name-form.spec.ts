import { ComponentFixture, TestBed } from '@angular/core/testing';

import { PersonDetails } from './person';
import { PersonNameForm } from './person-name-form';

describe('PersonNameForm', () => {
  let fixture: ComponentFixture<PersonNameForm>;
  let saved: PersonDetails[];

  beforeEach(async () => {
    fixture = TestBed.createComponent(PersonNameForm);
    saved = [];
    fixture.componentInstance.saved.subscribe((details) => saved.push(details));
    await fixture.whenStable();
  });

  const input = (name: string) =>
    fixture.nativeElement.querySelector(`input[formControlName=${name}]`) as HTMLInputElement;

  const type = (name: string, value: string) => {
    input(name).value = value;
    input(name).dispatchEvent(new Event('input'));
  };

  const submit = async () => {
    (fixture.nativeElement.querySelector('form') as HTMLFormElement).dispatchEvent(
      new Event('submit'),
    );
    await fixture.whenStable();
  };

  it('saves a first name alone, with blank optional parts as null', async () => {
    type('firstName', '  Maya ');
    type('lastName', '   ');
    await submit();

    expect(saved).toEqual([
      { name: { firstName: 'Maya', middleName: null, lastName: null }, gender: null },
    ]);
  });

  it('asks for a first name instead of saving', async () => {
    type('firstName', '   ');
    await submit();

    expect(saved).toEqual([]);
    expect(fixture.nativeElement.querySelector('[role=alert]')).not.toBeNull();
  });

  it('starts from the given name', async () => {
    fixture.componentRef.setInput('name', {
      firstName: 'Maya',
      middleName: 'Devi',
      lastName: 'Rai',
    });
    await fixture.whenStable();

    expect(input('firstName').value).toBe('Maya');
    expect(input('middleName').value).toBe('Devi');
    expect(input('lastName').value).toBe('Rai');
  });

  it('saves the chosen gender, which is optional (AC-01.21)', async () => {
    fixture.componentRef.setInput('gender', 'Male');
    await fixture.whenStable();
    const select = fixture.nativeElement.querySelector('select') as HTMLSelectElement;
    expect(select.selectedOptions[0].textContent?.trim()).toBe('Male');

    select.selectedIndex = Array.from(select.options).findIndex(
      (option) => option.textContent?.trim() === 'Female',
    );
    select.dispatchEvent(new Event('change'));
    type('firstName', 'Maya');
    await submit();

    expect(saved).toEqual([
      { name: { firstName: 'Maya', middleName: null, lastName: null }, gender: 'Female' },
    ]);
  });
});
