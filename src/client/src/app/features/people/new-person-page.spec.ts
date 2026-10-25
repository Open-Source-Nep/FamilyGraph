import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';

import { GraphService } from '../graph/graph.service';
import { MarriagesService } from '../relationships/marriages.service';
import { NewLink, Spouse } from '../relationships/relationship';
import { RelationshipsService } from '../relationships/relationships.service';
import { NewPersonPage } from './new-person-page';
import { PeopleService } from './people.service';
import { Person, PersonDetails, PersonName } from './person';

function person(id: string, firstName: string, extra: Partial<Person> = {}): Person {
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
    description: null,
    birthDate: null,
    birthDateShown: false,
    ...extra,
  };
}

const hari = person('p2', 'Hari', { maintainedByYou: true });
const sita = person('p3', 'Sita', { gender: 'Female' });
const kiran = person('p4', 'Kiran', { maintainedByYou: true });

describe('NewPersonPage', () => {
  let fixture: ComponentFixture<NewPersonPage>;
  let created: PersonName[];
  let links: NewLink[];
  let active: (link: NewLink) => boolean;
  let spouses: Record<string, Spouse[]>;
  let navigations: unknown[];

  beforeEach(async () => {
    created = [];
    links = [];
    active = () => true;
    spouses = {};
    navigations = [];
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: PeopleService,
          useValue: {
            create: ({ name }: PersonDetails) => {
              created.push(name);
              return Promise.resolve({ ...person('p1', name.firstName), ...name });
            },
            listMaintained: () => Promise.resolve({ people: [hari, kiran], isTruncated: false }),
            search: () => Promise.resolve({ people: [], isTruncated: false }),
          },
        },
        {
          provide: GraphService,
          useValue: {
            overview: () =>
              Promise.resolve({ people: [sita], links: [], marriages: [], isTruncated: false }),
          },
        },
        {
          provide: RelationshipsService,
          useValue: {
            create: (link: NewLink) => {
              links.push(link);
              return Promise.resolve({ ...link, id: 'l' + links.length, isActive: active(link) });
            },
          },
        },
        {
          provide: MarriagesService,
          useValue: {
            listSpouses: (id: string) =>
              Promise.resolve({ spouses: spouses[id] ?? [], isTruncated: false }),
          },
        },
      ],
    });
    const router = TestBed.inject(Router);
    router.navigate = (commands: unknown[]) => {
      navigations.push(commands);
      return Promise.resolve(true);
    };
    fixture = TestBed.createComponent(NewPersonPage);
    await fixture.whenStable();
  });

  const element = () => fixture.nativeElement as HTMLElement;
  const text = () => element().textContent?.replace(/\s+/g, ' ') ?? '';
  const settle = async () => {
    await fixture.whenStable();
    await new Promise((resolve) => setTimeout(resolve));
    await fixture.whenStable();
  };
  const button = (label: string) =>
    [...element().querySelectorAll('button')].find(
      (candidate) => candidate.textContent?.trim() === label,
    ) as HTMLButtonElement;
  const pick = async (section: number, name: string) => {
    const picker = element().querySelectorAll('app-person-picker')[section];
    const label = [...picker.querySelectorAll('.options label')].find((candidate) =>
      candidate.textContent?.includes(name),
    );
    label?.querySelector('input')?.click();
    await settle();
  };
  const enterName = async (name: string) => {
    const first = element().querySelector('input[formcontrolname=firstName]') as HTMLInputElement;
    first.value = name;
    first.dispatchEvent(new Event('input'));
    await fixture.whenStable();
  };

  it('still adds a person with only a first name (AC-01.1)', async () => {
    await enterName('Maya');

    button('Add person').click();
    await settle();

    expect(created).toEqual([{ firstName: 'Maya', middleName: null, lastName: null }]);
    expect(links).toEqual([]);
    expect(navigations).toEqual([['/people', 'p1']]);
  });

  it('links the chosen parents, their ticked spouses and children (AC-01.29, AC-08.15)', async () => {
    spouses['p2'] = [{ ...sita, marriageId: 'm1', isFormer: false }];
    await enterName('Maya');
    button('Choose parents').click();
    await settle();
    await pick(0, 'Hari');
    expect(text()).toContain('Sita, wife of Hari');
    button('Choose children').click();
    await settle();
    await pick(1, 'Kiran');
    [
      ...element().querySelectorAll<HTMLInputElement>('input[name=new-person-child-type]'),
    ][1].click();
    await fixture.whenStable();

    button('Add person').click();
    await settle();

    expect(links).toEqual([
      { parentId: 'p2', childId: 'p1', type: 'Biological' },
      { parentId: 'p3', childId: 'p1', type: 'Biological' },
      { parentId: 'p1', childId: 'p4', type: 'Adoptive' },
    ]);
    expect(navigations).toEqual([['/people', 'p1']]);
  });

  it('stays to report links that wait for approval (AC-01.29)', async () => {
    active = (link) => link.parentId !== 'p3';
    await enterName('Maya');
    button('Choose parents').click();
    await settle();
    await pick(0, 'Sita');

    button('Add person').click();
    await settle();

    expect(navigations).toEqual([]);
    expect(text()).toContain('Maya was added.');
    expect(text()).toContain('Sita as a parent: saved, but not active until the other maintainer');
    expect(element().querySelector('a')?.getAttribute('href')).toBe('/people/p1');
  });
});
