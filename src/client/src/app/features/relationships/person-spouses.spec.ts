import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { PeopleService } from '../people/people.service';
import { Person } from '../people/person';
import { MarriagesService } from './marriages.service';
import { PersonSpouses } from './person-spouses';
import { NewMarriage, Spouse, SpouseList } from './relationship';

const ram: Person = {
  id: 'p1',
  firstName: 'Ram',
  middleName: null,
  lastName: null,
  placeholder: null,
  visibility: 'Public',
  imageId: null,
  gender: 'Male',
  maintainedByYou: true,
  description: null,
  birthDate: null,
  birthDateShown: false,
};

const spouse = (
  id: string,
  firstName: string,
  isFormer: boolean,
  maintainedByYou: boolean,
): Spouse => ({
  marriageId: `m-${id}`,
  id,
  firstName,
  middleName: null,
  lastName: null,
  placeholder: null,
  visibility: 'Public',
  imageId: null,
  gender: 'Female',
  maintainedByYou,
  isFormer,
});

describe('PersonSpouses', () => {
  let fixture: ComponentFixture<PersonSpouses>;
  let list: SpouseList;
  let created: NewMarriage[];
  let marked: [string, boolean][];
  let disagreed: [string, string][];

  beforeEach(async () => {
    list = {
      spouses: [spouse('p2', 'Sita', false, false), spouse('p3', 'Gita', true, true)],
      isTruncated: false,
    };
    created = [];
    marked = [];
    disagreed = [];
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: MarriagesService,
          useValue: {
            listSpouses: () => Promise.resolve(list),
            create: (marriage: NewMarriage) => {
              created.push(marriage);
              return Promise.resolve({
                id: 'm9',
                firstSpouseId: 'a',
                secondSpouseId: 'b',
                isActive: false,
              });
            },
            setFormer: (marriageId: string, isFormer: boolean) => {
              marked.push([marriageId, isFormer]);
              return Promise.resolve();
            },
            disagree: (marriageId: string, spouseId: string) => {
              disagreed.push([marriageId, spouseId]);
              return Promise.resolve({});
            },
            delete: () => Promise.resolve(),
          },
        },
        {
          provide: PeopleService,
          useValue: {
            listMaintained: () =>
              Promise.resolve({
                people: [ram, { id: 'p4', firstName: 'Lila', middleName: null, lastName: null }],
                isTruncated: false,
              }),
          },
        },
      ],
    });
    fixture = TestBed.createComponent(PersonSpouses);
    fixture.componentRef.setInput('person', ram);
    await fixture.whenStable();
  });

  const text = () => (fixture.nativeElement as HTMLElement).textContent?.replace(/\s+/g, ' ') ?? '';

  const buttons = (label: string) =>
    [...fixture.nativeElement.querySelectorAll('button')].filter(
      (element: HTMLButtonElement) => element.textContent?.trim() === label,
    ) as HTMLButtonElement[];

  it('lists current spouses first and names a former one (AC-02.15, AC-08.9)', () => {
    expect(text()).toContain('Sita (wife)');
    expect(text()).toContain('Gita (former wife)');
  });

  it('marks a marriage former or current without consent (AC-02.14)', async () => {
    buttons('Mark as former')[0].click();
    await fixture.whenStable();
    buttons('Mark as current')[0].click();
    await fixture.whenStable();

    expect(marked).toEqual([
      ['m-p2', true],
      ['m-p3', false],
    ]);
  });

  it('withdraws your side when you maintain only one spouse, and offers delete when both (AC-02.12)', async () => {
    expect(buttons('Delete marriage').length).toBe(1);
    buttons('Withdraw approval')[0].click();
    await fixture.whenStable();
    buttons('Withdraw')[0].click();
    await fixture.whenStable();

    expect(disagreed).toEqual([['m-p2', 'p1']]);
    expect(text()).toContain('no longer active');
  });

  it('marries one of your other profiles to this person (AC-02.11)', async () => {
    buttons('Add a spouse')[0].click();
    await fixture.whenStable();
    expect(text()).not.toContain('Ram Ram');
    (fixture.nativeElement.querySelector('input[type=radio]') as HTMLInputElement).click();
    await fixture.whenStable();
    buttons('Add marriage')[0].click();
    await fixture.whenStable();

    expect(created).toEqual([{ spouseId: 'p4', otherSpouseId: 'p1' }]);
    expect(text()).toContain('not active yet');
  });
});
