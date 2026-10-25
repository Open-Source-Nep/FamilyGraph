import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { GraphService } from '../graph/graph.service';
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
          provide: GraphService,
          useValue: {
            overview: () =>
              Promise.resolve({ people: [], links: [], marriages: [], isTruncated: false }),
          },
        },
        {
          provide: PeopleService,
          useValue: {
            create: () => Promise.resolve({ ...ram, id: 'p8', firstName: 'Kamala', gender: null }),
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

  const openForm = async () => {
    buttons('Add a spouse')[0].click();
    // The picker loads from two stubbed requests, outside Angular's view of pending work.
    await fixture.whenStable();
    await new Promise((resolve) => setTimeout(resolve));
    await fixture.whenStable();
  };

  it('marries one of your other profiles to this person (AC-02.11)', async () => {
    await openForm();
    expect(text()).not.toContain('Ram (yours)');
    (fixture.nativeElement.querySelector('input[type=radio]') as HTMLInputElement).click();
    await fixture.whenStable();
    buttons('Add marriage')[0].click();
    await fixture.whenStable();

    expect(created).toEqual([{ spouseId: 'p4', otherSpouseId: 'p1' }]);
    expect(text()).toContain('not active yet');
  });

  it('adds a new person as the spouse with the shared picker (AC-08.17)', async () => {
    await openForm();
    buttons('Add a new person')[0].click();
    await fixture.whenStable();
    const dialog = fixture.nativeElement.querySelector('dialog') as HTMLDialogElement;
    const firstName = dialog.querySelector('input[formControlName=firstName]') as HTMLInputElement;
    firstName.value = 'Kamala';
    firstName.dispatchEvent(new Event('input'));
    (dialog.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    await fixture.whenStable();

    expect(dialog.open).toBe(false);
    const checked = fixture.nativeElement.querySelector(
      'input[type=radio]:checked',
    ) as HTMLInputElement;
    expect(checked.value).toBe('p8');
    buttons('Add marriage')[0].click();
    await fixture.whenStable();

    expect(created).toEqual([{ spouseId: 'p8', otherSpouseId: 'p1' }]);
  });
});
