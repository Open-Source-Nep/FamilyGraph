import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { RelationshipsService } from '../relationships/relationships.service';
import { PeopleService } from './people.service';
import { Person } from './person';
import { PersonPage } from './person-page';

describe('PersonPage', () => {
  let fixture: ComponentFixture<PersonPage>;
  let person: Person;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: PeopleService, useValue: { get: () => Promise.resolve(person) } },
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

  const show = async (maintainedByYou: boolean) => {
    person = { id: 'p1', firstName: 'Maya', middleName: null, lastName: 'Rai', maintainedByYou };
    fixture = TestBed.createComponent(PersonPage);
    fixture.componentRef.setInput('id', 'p1');
    await fixture.whenStable();
  };

  const editButton = () =>
    [...fixture.nativeElement.querySelectorAll('button')].find(
      (button: HTMLButtonElement) => button.textContent?.trim() === 'Edit name',
    );

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
});
