import { ComponentFixture, TestBed } from '@angular/core/testing';

import { PeopleService } from '../people/people.service';
import { Person, PersonName } from '../people/person';
import { MarriagesService } from '../relationships/marriages.service';
import { NewLink, NewMarriage } from '../relationships/relationship';
import { RelationshipsService } from '../relationships/relationships.service';
import { AddRelatives, AddRelativesResult, matchesName } from './add-relatives';
import { GraphPerson } from './graph';
import { AddRelation } from './graph-diagram';
import { GraphService } from './graph.service';

function person(id: string, firstName: string | null, extra: Partial<Person> = {}): Person {
  return {
    id,
    firstName,
    middleName: null,
    lastName: null,
    placeholder: null,
    visibility: 'Public',
    imageId: null,
    gender: null,
    maintainedByYou: false,
    ...extra,
  };
}

const maya = person('p1', 'Maya', { maintainedByYou: true });
const asha = { id: 'p2', firstName: 'Asha', middleName: null, lastName: 'Rai' };
const ravi = person('p3', 'Ravi');
const hidden = person('p4', null, { placeholder: 'Private', visibility: 'Private' });
const zarina = person('p5', 'Zarina', { lastName: 'Gurung' });

describe('matchesName', () => {
  it('needs every word in some name part, ignoring case', () => {
    const name: PersonName = { firstName: 'Maya', middleName: 'Devi', lastName: 'Rai' };
    expect(matchesName(name, 'rai MAY')).toBe(true);
    expect(matchesName(name, 'maya gurung')).toBe(false);
    expect(matchesName(name, '  ')).toBe(true);
  });
});

describe('AddRelatives', () => {
  let fixture: ComponentFixture<AddRelatives>;
  let links: NewLink[];
  let marriages: NewMarriage[];
  let searches: string[];
  let overviews: number;
  let active: boolean;
  let rejected: Set<string>;
  let results: AddRelativesResult[];

  async function open(target: GraphPerson, relation: AddRelation): Promise<void> {
    fixture = TestBed.createComponent(AddRelatives);
    fixture.componentRef.setInput('person', target);
    fixture.componentRef.setInput('relation', relation);
    fixture.componentInstance.closed.subscribe((result) => results.push(result));
    await fixture.whenStable();
    // The connections load from two stubbed requests, outside Angular's view of pending work.
    await new Promise((resolve) => setTimeout(resolve));
    await fixture.whenStable();
  }

  beforeEach(() => {
    links = [];
    marriages = [];
    searches = [];
    overviews = 0;
    active = true;
    rejected = new Set();
    results = [];
    const create = (id: string) =>
      rejected.has(id) ? Promise.reject(new Error('cycle')) : Promise.resolve({ isActive: active });
    TestBed.configureTestingModule({
      providers: [
        {
          provide: PeopleService,
          useValue: {
            listMaintained: () =>
              Promise.resolve({
                people: [
                  { ...maya, visibility: 'Public' },
                  { ...asha, visibility: 'Private' },
                ],
                isTruncated: false,
              }),
            search: (text: string) => {
              searches.push(text);
              return Promise.resolve({ people: [zarina, maya], isTruncated: true });
            },
            create: (name: PersonName) => Promise.resolve({ ...person('p9', null), ...name }),
          },
        },
        {
          provide: GraphService,
          useValue: {
            overview: () => {
              overviews++;
              return Promise.resolve({
                people: [maya, ravi, hidden],
                links: [],
                marriages: [],
                isTruncated: false,
              });
            },
          },
        },
        {
          provide: RelationshipsService,
          useValue: {
            create: (link: NewLink) => {
              links.push(link);
              return create(link.parentId === 'p1' ? link.childId : link.parentId);
            },
          },
        },
        {
          provide: MarriagesService,
          useValue: {
            create: (marriage: NewMarriage) => {
              marriages.push(marriage);
              return create(marriage.otherSpouseId);
            },
          },
        },
      ],
    });
  });

  const element = () => fixture.nativeElement as HTMLElement;
  const text = () => element().textContent ?? '';
  const optionLabels = () =>
    [...element().querySelectorAll('.options label')].map((label) =>
      label.textContent?.replace(/\s+/g, ' ').trim(),
    );
  const button = (label: string) =>
    [...element().querySelectorAll('button')].find(
      (candidate) => candidate.textContent?.trim() === label,
    ) as HTMLButtonElement;
  const pick = async (name: string) => {
    const label = [...element().querySelectorAll('.options label')].find((candidate) =>
      candidate.textContent?.includes(name),
    );
    label?.querySelector('input')?.click();
    await fixture.whenStable();
  };
  const type = async (value: string) => {
    const input = element().querySelector('input[type=search]') as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    await new Promise((resolve) => setTimeout(resolve, 350));
    await fixture.whenStable();
  };

  it('lists your profiles and the people linked to them, never the person or a placeholder (AC-08.11)', async () => {
    await open(maya, 'parent');

    expect(text()).toContain('Add parents of Maya');
    expect(optionLabels()).toEqual(['Asha Rai (yours)', 'Ravi']);
  });

  it('searches everyone public as you type and says to refine a long result (AC-08.11)', async () => {
    await open(maya, 'child');

    await type('ra');

    expect(searches).toEqual(['ra']);
    expect(optionLabels()).toEqual(['Asha Rai (yours)', 'Ravi', 'Zarina Gurung']);
    expect(text()).toContain('More people match. Refine your search.');
  });

  it("offers only your profiles on someone else's person (AC-08.12)", async () => {
    await open(ravi, 'parent');

    await type('zar');

    expect(overviews).toBe(0);
    expect(searches).toEqual([]);
    expect(optionLabels()).toEqual([]);
    await type('');
    expect(optionLabels()).toEqual(['Maya (yours)', 'Asha Rai (yours)']);
    expect(text()).toContain('you can only add your own profiles');
  });

  it('links each picked parent with the chosen type and reports pending ones (AC-08.10, AC-02.1)', async () => {
    active = false;
    await open(maya, 'parent');
    await pick('Asha');
    await pick('Ravi');
    (element().querySelector('input[value=Adoptive]') as HTMLInputElement).click();

    button('Add 2').click();
    await fixture.whenStable();

    expect(links).toEqual([
      { parentId: 'p2', childId: 'p1', type: 'Adoptive' },
      { parentId: 'p3', childId: 'p1', type: 'Adoptive' },
    ]);
    expect(results).toEqual([
      {
        changed: true,
        message: '2 links are saved but not active until the other maintainers approve them.',
      },
    ]);
  });

  it('marries one spouse at a time (AC-08.10, AC-02.11)', async () => {
    await open(maya, 'spouse');
    await pick('Asha');
    await pick('Ravi');

    button('Add spouse').click();
    await fixture.whenStable();

    expect(marriages).toEqual([{ spouseId: 'p1', otherSpouseId: 'p3' }]);
    expect(results[0]).toEqual({ changed: true, message: 'Linked 1 person to Maya.' });
  });

  it('adds a new person you maintain and selects them (AC-08.13)', async () => {
    await open(maya, 'child');
    button('Add a new person').click();
    await fixture.whenStable();
    const first = element().querySelector('input[formcontrolname=firstName]') as HTMLInputElement;
    first.value = 'Kiran';
    first.dispatchEvent(new Event('input'));
    button('Add person').click();
    await fixture.whenStable();

    expect(text()).toContain('Selected: Kiran');
    button('Add').click();
    await fixture.whenStable();

    expect(links).toEqual([{ parentId: 'p1', childId: 'p9', type: 'Biological' }]);
  });

  it('keeps the dialog open with the reason when a link is refused', async () => {
    rejected.add('p3');
    await open(maya, 'child');
    await pick('Ravi');

    button('Add').click();
    await fixture.whenStable();

    expect(results).toEqual([]);
    expect(element().querySelector('[role=alert]')?.textContent).toBeTruthy();
    expect(text()).toContain('Selected: Ravi');
  });

  it('asks for a person before adding', async () => {
    await open(maya, 'parent');

    button('Add').click();
    await fixture.whenStable();

    expect(links).toEqual([]);
    expect(text()).toContain('Choose at least one person.');
  });

  it('tells nothing changed when cancelled', async () => {
    await open(maya, 'parent');

    button('Cancel').click();

    expect(results).toEqual([{ changed: false, message: null }]);
  });
});
