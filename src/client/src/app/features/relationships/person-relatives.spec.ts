import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { PeopleService } from '../people/people.service';
import { Person, PersonName } from '../people/person';
import { PersonRelatives } from './person-relatives';
import { CreatedLink, DirectRelatives, LinkEndpoint, NewLink } from './relationship';
import { RelationshipsService } from './relationships.service';

const maya: Person = {
  id: 'p1',
  firstName: 'Maya',
  middleName: null,
  lastName: null,
  placeholder: null,
  visibility: 'Public',
  imageId: null,
  gender: null,
  maintainedByYou: true,
};

const relatives: DirectRelatives = {
  parents: [
    {
      linkId: 'l2',
      id: 'p2',
      firstName: 'Hari',
      middleName: null,
      lastName: 'Rai',
      placeholder: null,
      visibility: 'Public',
      imageId: null,
      gender: null,
      type: 'Adoptive',
      maintainedByYou: true,
    },
    {
      linkId: 'l3',
      id: 'p3',
      firstName: 'Sita',
      middleName: null,
      lastName: null,
      placeholder: null,
      visibility: 'Public',
      imageId: null,
      gender: 'Female',
      type: 'Biological',
      maintainedByYou: false,
    },
  ],
  parentsTruncated: false,
  children: [],
  childrenTruncated: false,
};

describe('PersonRelatives', () => {
  let fixture: ComponentFixture<PersonRelatives>;
  let created: NewLink[];
  let disagreed: [string, LinkEndpoint][];
  let deleted: string[];
  let active: boolean;
  let rejected: Set<string>;
  let newPeople: PersonName[];

  beforeEach(async () => {
    created = [];
    disagreed = [];
    deleted = [];
    active = true;
    rejected = new Set();
    newPeople = [];
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: RelationshipsService,
          useValue: {
            listDirectRelatives: () => Promise.resolve(relatives),
            create: (link: NewLink): Promise<CreatedLink> => {
              if (rejected.has(link.parentId) || rejected.has(link.childId)) {
                return Promise.reject(new Error('cycle'));
              }
              created.push(link);
              return Promise.resolve({ ...link, id: 'l1', isActive: active });
            },
            disagree: (linkId: string, endpoint: LinkEndpoint) => {
              disagreed.push([linkId, endpoint]);
              return Promise.resolve({});
            },
            delete: (linkId: string) => {
              deleted.push(linkId);
              return Promise.resolve();
            },
          },
        },
        {
          provide: PeopleService,
          useValue: {
            listMaintained: () =>
              Promise.resolve({
                people: [
                  maya,
                  { id: 'p4', firstName: 'Asha', middleName: null, lastName: null },
                  { id: 'p5', firstName: 'Bina', middleName: null, lastName: 'Rai' },
                ],
                isTruncated: false,
              }),
            create: (name: PersonName) => {
              newPeople.push(name);
              return Promise.resolve({ ...name, id: 'p6', maintainedByYou: true });
            },
          },
        },
      ],
    });
    fixture = TestBed.createComponent(PersonRelatives);
    fixture.componentRef.setInput('person', maya);
    await fixture.whenStable();
  });

  const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';

  const button = (label: string) =>
    [...fixture.nativeElement.querySelectorAll('button')].find(
      (element: HTMLButtonElement) => element.textContent?.trim() === label,
    ) as HTMLButtonElement;

  const openForm = async () => {
    button('Add parents or children').click();
    await fixture.whenStable();
  };

  const choose = async (selector: string) => {
    (fixture.nativeElement.querySelector(selector) as HTMLInputElement).click();
    await fixture.whenStable();
  };

  const checkboxLabels = () =>
    [...fixture.nativeElement.querySelectorAll('input[type=checkbox]')].map(
      (input: HTMLInputElement) => input.parentElement?.textContent?.trim(),
    );

  it('lists parents, naming adoptive ones and genders where known (AC-08.7, AC-08.8)', () => {
    const items = [...fixture.nativeElement.querySelectorAll('li')].map((li: HTMLElement) =>
      li.textContent?.replace(/\s+/g, ' ').trim(),
    );

    expect(items).toEqual([
      'Hari Rai (adoptive parent) Delete link',
      'Sita (mother) Withdraw approval',
    ]);
    expect(text()).toContain('No children are linked.');
  });

  it('offers only your other profiles', async () => {
    await openForm();

    expect(checkboxLabels()).toEqual(['Asha', 'Bina Rai']);
  });

  it('creates an adoptive child link', async () => {
    await openForm();
    await choose('input[value="child"]');
    await choose('input[value="p4"]');
    await choose('input[value="Adoptive"]');

    button('Add link').click();
    await fixture.whenStable();

    expect(created).toEqual([{ parentId: 'p1', childId: 'p4', type: 'Adoptive' }]);
  });

  it('asks for a profile instead of submitting none', async () => {
    await openForm();

    button('Add link').click();
    await fixture.whenStable();

    expect(created).toEqual([]);
    expect(text()).toContain('Choose at least one profile.');
  });

  it('says when the new link waits for approval', async () => {
    active = false;
    await openForm();
    await choose('input[value="p4"]');

    button('Add link').click();
    await fixture.whenStable();

    expect(created).toEqual([{ parentId: 'p4', childId: 'p1', type: 'Biological' }]);
    expect(text()).toContain('not active yet');
  });

  it('links several parents in one submit', async () => {
    await openForm();
    await choose('input[value="p4"]');
    await choose('input[value="p5"]');

    button('Add 2 links').click();
    await fixture.whenStable();

    expect(created).toEqual([
      { parentId: 'p4', childId: 'p1', type: 'Biological' },
      { parentId: 'p5', childId: 'p1', type: 'Biological' },
    ]);
    expect(text()).toContain('Asha: linked.');
    expect(text()).toContain('Bina Rai: linked.');
    // Every link was added, so the form closes.
    expect(button('Add parents or children')).toBeTruthy();
  });

  it('keeps the profiles whose link failed selected', async () => {
    rejected.add('p4');
    await openForm();
    await choose('input[value="p4"]');
    await choose('input[value="p5"]');

    button('Add 2 links').click();
    await fixture.whenStable();

    expect(created).toEqual([{ parentId: 'p5', childId: 'p1', type: 'Biological' }]);
    expect(text()).toContain('Asha: not linked.');
    expect(text()).toContain('Bina Rai: linked.');
    const checked = [...fixture.nativeElement.querySelectorAll('input[type=checkbox]:checked')].map(
      (input: HTMLInputElement) => input.value,
    );
    expect(checked).toEqual(['p4']);
  });

  it('adds a new person from the dialog and selects them', async () => {
    await openForm();
    button('Add a new person').click();
    await fixture.whenStable();
    const dialog = fixture.nativeElement.querySelector('dialog') as HTMLDialogElement;
    expect(dialog.open).toBe(true);

    const firstName = dialog.querySelector('input[formControlName=firstName]') as HTMLInputElement;
    firstName.value = 'Kiran';
    firstName.dispatchEvent(new Event('input'));
    (dialog.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    await fixture.whenStable();

    expect(newPeople).toEqual([{ firstName: 'Kiran', middleName: null, lastName: null }]);
    expect(dialog.open).toBe(false);
    expect(checkboxLabels()).toEqual(['Asha', 'Bina Rai', 'Kiran']);

    button('Add link').click();
    await fixture.whenStable();

    expect(created).toEqual([{ parentId: 'p6', childId: 'p1', type: 'Biological' }]);
  });

  it('leaves the link form as it was when the dialog is cancelled', async () => {
    await openForm();
    await choose('input[value="p4"]');
    button('Add a new person').click();
    await fixture.whenStable();

    const dialog = fixture.nativeElement.querySelector('dialog') as HTMLDialogElement;
    (dialog.querySelector('button.secondary') as HTMLButtonElement).click();
    await fixture.whenStable();

    expect(dialog.open).toBe(false);
    expect(newPeople).toEqual([]);
    expect(button('Add link')).toBeTruthy();
  });

  it('withdraws your approval of a link to a profile someone else maintains after confirming', async () => {
    button('Withdraw approval').click();
    await fixture.whenStable();
    expect(text()).toContain('Withdraw your approval?');

    button('Withdraw').click();
    await fixture.whenStable();

    // Maya is the child on the link to Sita, and you maintain Maya.
    expect(disagreed).toEqual([['l3', 'Child']]);
    expect(text()).toContain('no longer active');
  });

  it('deletes a link whose two people you maintain after confirming', async () => {
    // You maintain both Maya and Hari, so the link can be deleted but not withdrawn (D-014).
    expect(button('Delete link')).toBeTruthy();
    button('Delete link').click();
    await fixture.whenStable();
    expect(text()).toContain('Delete this link?');
    expect(deleted).toEqual([]);

    button('Delete').click();
    await fixture.whenStable();

    expect(deleted).toEqual(['l2']);
    expect(disagreed).toEqual([]);
    expect(text()).toContain('You deleted the link to Hari Rai.');
  });

  it('offers withdrawal, not deletion, when you maintain only the relative', async () => {
    fixture.componentRef.setInput('person', { ...maya, id: 'p9', maintainedByYou: false });
    await fixture.whenStable();

    // You maintain Hari only, and neither Maya nor Sita here, so only Hari's link can be withdrawn.
    const labels = [...fixture.nativeElement.querySelectorAll('button')].map(
      (element: HTMLButtonElement) => element.textContent?.trim(),
    );
    expect(labels.filter((label) => label === 'Withdraw approval')).toHaveLength(1);
    expect(labels).not.toContain('Delete link');
  });
});
