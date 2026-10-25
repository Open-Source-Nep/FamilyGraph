import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { PeopleService } from '../people/people.service';
import { Person } from '../people/person';
import { MarriagesService } from './marriages.service';
import { PersonRelatives } from './person-relatives';
import { Child, DirectRelatives } from './relationship';
import { RelationshipsService } from './relationships.service';

const hari: Person = {
  id: 'p1',
  firstName: 'Hari',
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

function child(linkId: string, firstName: string, birthOrder: number | null): Child {
  return {
    linkId,
    id: `c-${linkId}`,
    firstName,
    middleName: null,
    lastName: null,
    placeholder: null,
    visibility: 'Public',
    imageId: null,
    gender: null,
    type: 'Biological',
    maintainedByYou: false,
    birthOrder,
  };
}

describe('PersonRelatives birth order', () => {
  let fixture: ComponentFixture<PersonRelatives>;
  let orders: [string, readonly string[]][];

  const setUp = async (person: Person) => {
    orders = [];
    const relatives: DirectRelatives = {
      parents: [],
      parentsTruncated: false,
      children: [child('l1', 'Maya', 1), child('l2', 'Asha', null), child('l3', 'Gita', null)],
      childrenTruncated: false,
    };
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: MarriagesService,
          useValue: { listSpouses: () => Promise.resolve({ spouses: [], isTruncated: false }) },
        },
        {
          provide: RelationshipsService,
          useValue: {
            listDirectRelatives: () => Promise.resolve(relatives),
            orderChildren: (parentId: string, linkIds: readonly string[]) => {
              orders.push([parentId, linkIds]);
              return Promise.resolve();
            },
          },
        },
        { provide: PeopleService, useValue: {} },
      ],
    });
    fixture = TestBed.createComponent(PersonRelatives);
    fixture.componentRef.setInput('person', person);
    await fixture.whenStable();
  };

  const names = () =>
    [...fixture.nativeElement.querySelectorAll('li a')].map((link: HTMLElement) =>
      link.textContent?.trim(),
    );

  const arrow = (label: string) =>
    fixture.nativeElement.querySelector(`button[aria-label="${label}"]`) as HTMLButtonElement;

  it('moves a child down and numbers the whole list eldest first (AC-02.8)', async () => {
    await setUp(hari);
    expect(arrow('Move Maya up').disabled).toBe(true);
    expect(arrow('Move Gita down').disabled).toBe(true);

    arrow('Move Maya down').click();
    await fixture.whenStable();

    expect(orders).toEqual([['p1', ['l2', 'l1', 'l3']]]);
    expect(names()).toEqual(['Asha', 'Maya', 'Gita']);
  });

  it('offers no arrows on a parent you do not maintain (AC-02.9)', async () => {
    await setUp({ ...hari, maintainedByYou: false });

    expect(fixture.nativeElement.querySelector('button.order')).toBeNull();
  });
});
