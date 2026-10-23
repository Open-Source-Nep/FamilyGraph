import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { RelationshipsService } from '../relationships/relationships.service';
import { PeopleService } from './people.service';
import { Person } from './person';
import { PersonPage } from './person-page';

describe('PersonPage', () => {
  let fixture: ComponentFixture<PersonPage>;
  let person: Person;
  let visibilityCalls: [string, string][];

  beforeEach(() => {
    visibilityCalls = [];
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: PeopleService,
          useValue: {
            get: () => Promise.resolve(person),
            setVisibility: (id: string, visibility: string) => {
              visibilityCalls.push([id, visibility]);
              return Promise.resolve();
            },
          },
        },
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
      maintainedByYou,
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
  const editButton = () => button('Edit name');

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

  it('shows a private placeholder without name details', async () => {
    await show(false, {
      firstName: null,
      lastName: null,
      placeholder: 'Private',
      visibility: 'Private',
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
});
