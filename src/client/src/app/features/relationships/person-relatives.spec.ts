import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { PeopleService } from '../people/people.service';
import { Person } from '../people/person';
import { PersonRelatives } from './person-relatives';
import { CreatedLink, DirectRelatives, NewLink } from './relationship';
import { RelationshipsService } from './relationships.service';

const maya: Person = {
  id: 'p1',
  firstName: 'Maya',
  middleName: null,
  lastName: null,
  maintainedByYou: true,
};

const relatives: DirectRelatives = {
  parents: [
    { id: 'p2', firstName: 'Hari', middleName: null, lastName: 'Rai', type: 'Adoptive' },
    { id: 'p3', firstName: 'Sita', middleName: null, lastName: null, type: 'Biological' },
  ],
  parentsTruncated: false,
  children: [],
  childrenTruncated: false,
};

describe('PersonRelatives', () => {
  let fixture: ComponentFixture<PersonRelatives>;
  let created: NewLink[];
  let active: boolean;

  beforeEach(async () => {
    created = [];
    active = true;
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: RelationshipsService,
          useValue: {
            listDirectRelatives: () => Promise.resolve(relatives),
            create: (link: NewLink): Promise<CreatedLink> => {
              created.push(link);
              return Promise.resolve({ ...link, id: 'l1', isActive: active });
            },
          },
        },
        {
          provide: PeopleService,
          useValue: {
            listMaintained: () =>
              Promise.resolve({
                people: [maya, { id: 'p4', firstName: 'Asha', middleName: null, lastName: null }],
                isTruncated: false,
              }),
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
    button('Add a parent or child').click();
    await fixture.whenStable();
  };

  const choose = async (selector: string, value: string) => {
    const element = fixture.nativeElement.querySelector(selector) as
      HTMLInputElement | HTMLSelectElement;
    if (element instanceof HTMLInputElement) {
      element.click();
    } else {
      element.value = value;
      element.dispatchEvent(new Event('change'));
    }
    await fixture.whenStable();
  };

  it('lists parents and marks adoptive ones', () => {
    const items = [...fixture.nativeElement.querySelectorAll('li')].map((li: HTMLElement) =>
      li.textContent?.replace(/\s+/g, ' ').trim(),
    );

    expect(items).toEqual(['Hari Rai (adoptive parent)', 'Sita']);
    expect(text()).toContain('No children are linked.');
  });

  it('offers only your other profiles', async () => {
    await openForm();

    const options = [...fixture.nativeElement.querySelectorAll('option')].map(
      (option: HTMLOptionElement) => option.textContent?.trim(),
    );
    expect(options).toEqual(['Choose a profile you maintain', 'Asha']);
  });

  it('creates an adoptive child link', async () => {
    await openForm();
    await choose('input[value="child"]', '');
    await choose('select', 'p4');
    await choose('input[value="Adoptive"]', '');

    button('Add link').click();
    await fixture.whenStable();

    expect(created).toEqual([{ parentId: 'p1', childId: 'p4', type: 'Adoptive' }]);
  });

  it('says when the new link waits for approval', async () => {
    active = false;
    await openForm();
    await choose('select', 'p4');

    button('Add link').click();
    await fixture.whenStable();

    expect(created).toEqual([{ parentId: 'p4', childId: 'p1', type: 'Biological' }]);
    expect(text()).toContain('not active yet');
  });
});
