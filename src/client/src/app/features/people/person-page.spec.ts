import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { HandoversService } from '../handovers/handovers.service';
import { RelationshipsService } from '../relationships/relationships.service';
import { PeopleService } from './people.service';
import { Person } from './person';
import { PersonPage } from './person-page';

describe('PersonPage', () => {
  let fixture: ComponentFixture<PersonPage>;
  let person: Person;
  let visibilityCalls: [string, string][];
  let genderCalls: [string, string | null][];
  let detailCalls: unknown[][];
  let renamed: string[];
  let deleted: string[];
  let removedImages: string[];
  const isAdministrator = signal(false);

  beforeEach(() => {
    visibilityCalls = [];
    genderCalls = [];
    detailCalls = [];
    renamed = [];
    deleted = [];
    removedImages = [];
    isAdministrator.set(false);
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: PeopleService,
          useValue: {
            get: () => Promise.resolve(person),
            delete: (id: string) => {
              deleted.push(id);
              person = {
                ...person,
                firstName: null,
                middleName: null,
                lastName: null,
                placeholder: 'Deleted',
                maintainedByYou: false,
              };
              return Promise.resolve();
            },
            removeImage: (id: string) => {
              removedImages.push(id);
              return Promise.resolve();
            },
            rename: (id: string) => {
              renamed.push(id);
              return Promise.resolve();
            },
            setGender: (id: string, gender: string | null) => {
              genderCalls.push([id, gender]);
              return Promise.resolve();
            },
            setDescription: (...args: unknown[]) => {
              detailCalls.push(['description', ...args]);
              return Promise.resolve();
            },
            setBirthDate: (...args: unknown[]) => {
              detailCalls.push(['birthDate', ...args]);
              return Promise.resolve();
            },
            setVisibility: (id: string, visibility: string) => {
              visibilityCalls.push([id, visibility]);
              return Promise.resolve();
            },
          },
        },
        { provide: AuthService, useValue: { isAdministrator } },
        { provide: HandoversService, useValue: { getOpen: () => Promise.resolve(null) } },
        {
          provide: RelationshipsService,
          useValue: {
            listDirectRelatives: () =>
              Promise.resolve({
                parents: [],
                parentsTruncated: false,
                children: [],
                childrenTruncated: false,
              }),
          },
        },
      ],
    });
  });

  const show = async (maintainedByYou: boolean, changes: Partial<Person> = {}) => {
    person = {
      id: 'p1',
      firstName: 'Maya',
      middleName: null,
      lastName: 'Rai',
      placeholder: null,
      visibility: 'Public',
      imageId: null,
      gender: null,
      maintainedByYou,
      description: null,
      birthDate: null,
      birthDateShown: false,
      ...changes,
    };
    fixture = TestBed.createComponent(PersonPage);
    fixture.componentRef.setInput('id', 'p1');
    await fixture.whenStable();
  };

  const button = (text: string): HTMLButtonElement | undefined =>
    [...fixture.nativeElement.querySelectorAll('button')].find(
      (button: HTMLButtonElement) => button.textContent?.trim() === text,
    );
  const editButton = () => button('Edit details');

  it('shows the full name', async () => {
    await show(false);

    expect(fixture.nativeElement.querySelector('h1').textContent).toBe('Maya Rai');
  });

  it('offers editing to the maintainer only', async () => {
    await show(true);
    expect(editButton()).toBeDefined();

    await show(false);
    expect(editButton()).toBeUndefined();
  });

  it('shows the gender when it is set', async () => {
    await show(false);
    expect(fixture.nativeElement.querySelector('dl').textContent).not.toContain('Gender');

    await show(false, { gender: 'Female' });
    expect(fixture.nativeElement.querySelector('dl').textContent).toContain('GenderFemale');
  });

  // AC-01.22: the maintainer sets the gender; an unchanged name is not saved again.
  it('lets the maintainer set the gender with the details form', async () => {
    await show(true);
    editButton()!.click();
    await fixture.whenStable();

    const select = fixture.nativeElement.querySelector('select') as HTMLSelectElement;
    select.selectedIndex = Array.from(select.options).findIndex(
      (option) => option.textContent?.trim() === 'Other',
    );
    select.dispatchEvent(new Event('change'));
    button('Save')!.click();
    await fixture.whenStable();

    expect(genderCalls).toEqual([['p1', 'Other']]);
    expect(renamed).toEqual([]);
    expect(fixture.nativeElement.querySelector('dl').textContent).toContain('GenderOther');
  });

  it('shows the description and a birth date in both calendars (AC-01.30)', async () => {
    await show(false, {
      description: 'Teacher',
      birthDate: { calendar: 'Bs', year: 2082, month: 6, day: 16 },
      birthDateShown: true,
    });

    expect(fixture.nativeElement.querySelector('.description').textContent).toBe('Teacher');
    expect(fixture.nativeElement.querySelector('dl').textContent).toContain(
      'Born 16 Ashwin 2082 BS (2 October 2025)',
    );
  });

  it('tells the maintainer a birth date is hidden from others (AC-05.16)', async () => {
    await show(true, { birthDate: { calendar: 'Ad', year: 1990, month: null, day: null } });

    expect(fixture.nativeElement.querySelector('dl').textContent).toContain(
      '1990 (hidden from others)',
    );
  });

  // AC-01.31: only changed details are saved.
  it('saves the description and leaves an unchanged birth date alone', async () => {
    await show(true, { birthDate: { calendar: 'Ad', year: 1990, month: null, day: null } });
    editButton()!.click();
    await fixture.whenStable();

    const field = fixture.nativeElement.querySelector(
      'input[formControlName=description]',
    ) as HTMLInputElement;
    field.value = 'Engineer';
    field.dispatchEvent(new Event('input'));
    button('Save')!.click();
    await fixture.whenStable();

    expect(detailCalls).toEqual([['description', 'p1', 'Engineer']]);
    expect(fixture.nativeElement.querySelector('.description').textContent).toBe('Engineer');
  });

  it('offers a handover to the maintainer only', async () => {
    await show(true);
    expect(button('Create a handover link')).toBeDefined();

    await show(false);
    expect(button('Create a handover link')).toBeUndefined();
  });

  it('shows a private placeholder without name details', async () => {
    await show(false, {
      firstName: null,
      lastName: null,
      placeholder: 'Private',
      visibility: 'Private',
      imageId: null,
      gender: null,
    });

    expect(fixture.nativeElement.querySelector('h1').textContent).toBe('Private');
    expect(fixture.nativeElement.querySelector('dl')).toBeNull();
    expect(button('Make public')).toBeUndefined();
  });

  it('lets the maintainer make the profile private and public again', async () => {
    await show(true);

    button('Make private')!.click();
    await fixture.whenStable();
    expect(visibilityCalls).toEqual([['p1', 'Private']]);
    expect(fixture.nativeElement.textContent).toContain('others see only “Private”');

    button('Make public')!.click();
    await fixture.whenStable();
    expect(visibilityCalls).toEqual([
      ['p1', 'Private'],
      ['p1', 'Public'],
    ]);
  });

  it('offers no visibility change to others', async () => {
    await show(false);

    expect(button('Make private')).toBeUndefined();
  });

  it('lets the maintainer delete the profile after confirming', async () => {
    await show(true);

    button('Delete profile')!.click();
    await fixture.whenStable();
    expect(deleted).toEqual([]);
    button('Delete')!.click();
    await fixture.whenStable();

    expect(deleted).toEqual(['p1']);
    expect(fixture.nativeElement.querySelector('h1').textContent).toBe('Deleted');
    expect(fixture.nativeElement.textContent).toContain('This profile was deleted.');
    expect(button('Delete profile')).toBeUndefined();
  });

  it('offers deletion to administrators but not to other users', async () => {
    await show(false);
    expect(button('Delete profile')).toBeUndefined();

    isAdministrator.set(true);
    await show(false);
    expect(button('Delete profile')).toBeDefined();
  });

  const photo = (): HTMLImageElement | null => fixture.nativeElement.querySelector('img.photo');
  const label = (text: string): HTMLLabelElement | undefined =>
    [...fixture.nativeElement.querySelectorAll('label')].find(
      (label: HTMLLabelElement) => label.textContent?.trim() === text,
    );

  it('shows the photo to anyone who sees the profile', async () => {
    await show(false, { imageId: 'i1', gender: null });

    expect(photo()!.getAttribute('src')).toBe('/api/people/p1/image?v=i1');
    expect(photo()!.alt).toBe('Photo of Maya Rai');
    expect(label('Change photo')).toBeUndefined();
  });

  // AC-01.35: the photo opens enlarged and closes again.
  it('enlarges the photo on click and closes it', async () => {
    await show(false, { imageId: 'i1', gender: null });
    const dialog = (): HTMLDialogElement =>
      fixture.nativeElement.querySelector('dialog.photo-dialog');
    const enlarge = (): HTMLButtonElement =>
      fixture.nativeElement.querySelector('button.photo-button');
    expect(enlarge().getAttribute('aria-label')).toBe('Enlarge photo of Maya Rai');
    expect(dialog().open).toBe(false);

    enlarge().click();
    expect(dialog().open).toBe(true);
    expect(dialog().querySelector('img')!.getAttribute('src')).toBe('/api/people/p1/image?v=i1');

    button('Close')!.click();
    expect(dialog().open).toBe(false);

    enlarge().click();
    dialog().querySelector('img')!.click();
    expect(dialog().open).toBe(true);
    dialog().click();
    expect(dialog().open).toBe(false);
  });

  // AC-01.18: only the maintainer changes the photo.
  it('offers adding a photo to the maintainer only', async () => {
    await show(true);
    expect(photo()).toBeNull();
    expect(label('Add photo')!.querySelector('input')!.accept).toBe(
      'image/jpeg,image/png,image/webp',
    );
    expect(button('Remove photo')).toBeUndefined();

    await show(false);
    expect(label('Add photo')).toBeUndefined();
  });

  it('lets the maintainer remove the photo', async () => {
    await show(true, { imageId: 'i1', gender: null });

    button('Remove photo')!.click();
    await fixture.whenStable();

    expect(removedImages).toEqual(['p1']);
    expect(photo()).toBeNull();
    expect(label('Add photo')).toBeDefined();
  });
});
