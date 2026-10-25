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
      {
        name: { firstName: 'Maya', middleName: null, lastName: null },
        gender: null,
        description: null,
        birthDate: null,
        birthDateShown: false,
      },
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
      {
        name: { firstName: 'Maya', middleName: null, lastName: null },
        gender: 'Female',
        description: null,
        birthDate: null,
        birthDateShown: false,
      },
    ]);
  });

  const button = (label: string) =>
    Array.from(
      fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>,
    ).find((button) => button.textContent?.trim() === label);

  const choose = (label: string, option: string) => {
    const select = Array.from(
      fixture.nativeElement.querySelectorAll('.date-parts label') as NodeListOf<HTMLLabelElement>,
    )
      .find((element) => element.textContent?.trim().startsWith(label))!
      .querySelector('select') as HTMLSelectElement;
    select.value = option;
    select.dispatchEvent(new Event('change'));
  };

  const known = (label: string) => {
    const radio = Array.from(
      fixture.nativeElement.querySelectorAll('.known label') as NodeListOf<HTMLLabelElement>,
    )
      .find((element) => element.textContent?.trim() === label)!
      .querySelector('input') as HTMLInputElement;
    radio.dispatchEvent(new Event('change'));
  };

  const year = (value: string) => {
    const field = fixture.nativeElement.querySelector('.date-parts input') as HTMLInputElement;
    field.value = value;
    field.dispatchEvent(new Event('input'));
  };

  it('saves a one-line description and a partial BS birth date, hidden from others by default (AC-01.30)', async () => {
    type('firstName', 'Maya');
    type('description', '  Teacher,\n lives in Pokhara ');
    button('Add birth date')!.click();
    await fixture.whenStable();
    known('Year and month');
    await fixture.whenStable();
    year('2009');
    choose('Month', '1');
    await fixture.whenStable();
    await submit();

    expect(saved).toEqual([
      {
        name: { firstName: 'Maya', middleName: null, lastName: null },
        gender: null,
        description: 'Teacher, lives in Pokhara',
        birthDate: { calendar: 'Bs', year: 2009, month: 1, day: null },
        birthDateShown: false,
      },
    ]);
  });

  it('saves a full AD date shown to others (AC-01.32)', async () => {
    type('firstName', 'Maya');
    button('Add birth date')!.click();
    await fixture.whenStable();
    (
      fixture.nativeElement.querySelectorAll('input[type=radio]')[1] as HTMLInputElement
    ).dispatchEvent(new Event('change'));
    year('1952');
    choose('Month', '3');
    await fixture.whenStable();
    choose('Day', '4');
    await fixture.whenStable();
    input('birthDateShown').click();
    await submit();

    expect(saved[0].birthDate).toEqual({ calendar: 'Ad', year: 1952, month: 3, day: 4 });
    expect(saved[0].birthDateShown).toBe(true);
  });

  it('does not save a date that does not exist', async () => {
    fixture.componentRef.setInput('birthDate', { calendar: 'Ad', year: 1952, month: 2, day: 29 });
    await fixture.whenStable();
    type('firstName', 'Maya');
    year('1951');
    await fixture.whenStable();
    await submit();

    expect(saved).toEqual([]);
    expect(fixture.nativeElement.textContent).toContain('That date does not exist.');
  });

  it('saves only the year when only the year is known (AC-01.36)', async () => {
    type('firstName', 'Maya');
    button('Add birth date')!.click();
    await fixture.whenStable();
    known('Year only');
    await fixture.whenStable();
    year('1990');
    await submit();

    expect(fixture.nativeElement.querySelector('.date-parts select')).toBeNull();
    expect(saved[0].birthDate).toEqual({ calendar: 'Bs', year: 1990, month: null, day: null });
  });

  it('does not save until the known parts are chosen (AC-01.36)', async () => {
    type('firstName', 'Maya');
    button('Add birth date')!.click();
    await fixture.whenStable();
    year('2040');
    await fixture.whenStable();
    await submit();

    expect(saved).toEqual([]);
    expect(fixture.nativeElement.textContent).toContain(
      'Choose the month and day, or change what is known.',
    );
  });

  it('starts at the known level of the given date (AC-01.36)', async () => {
    fixture.componentRef.setInput('birthDate', { calendar: 'Ad', year: 1952, month: 3, day: null });
    await fixture.whenStable();

    const checked = fixture.nativeElement.querySelector('.known input:checked') as HTMLInputElement;
    expect(checked.parentElement!.textContent?.trim()).toBe('Year and month');
    expect(fixture.nativeElement.querySelectorAll('.date-parts select').length).toBe(1);
  });

  it('picks a year on the calendar when only the year is known (AC-01.36)', async () => {
    type('firstName', 'Maya');
    button('Add birth date')!.click();
    await fixture.whenStable();
    known('Year only');
    await fixture.whenStable();
    button('Pick on the calendar')!.click();
    await fixture.whenStable();
    const cells = Array.from(
      fixture.nativeElement.querySelectorAll('.osn-cal__period') as NodeListOf<HTMLButtonElement>,
    ).filter((cell) => !cell.disabled);
    cells[0].click();
    await fixture.whenStable();
    await submit();

    const date = saved[0].birthDate!;
    expect(date.month).toBeNull();
    expect(date.day).toBeNull();
    expect(Number.isInteger(date.year)).toBe(true);
    expect(cells[0].textContent).toContain(String(date.year));
  });

  it('starts from the given birth date and can remove it', async () => {
    fixture.componentRef.setInput('birthDate', { calendar: 'Bs', year: 2082, month: 6, day: 16 });
    fixture.componentRef.setInput('birthDateShown', true);
    await fixture.whenStable();
    type('firstName', 'Maya');
    button('Remove birth date')!.click();
    await fixture.whenStable();
    await submit();

    expect(saved[0].birthDate).toBeNull();
    expect(saved[0].birthDateShown).toBe(false);
  });
});
