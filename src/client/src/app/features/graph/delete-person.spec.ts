import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { PeopleService } from '../people/people.service';
import { DeletePerson } from './delete-person';
import { GraphPerson } from './graph';

const gita: GraphPerson = {
  id: 'gita',
  firstName: 'Gita',
  middleName: null,
  lastName: 'Rai',
  placeholder: null,
  visibility: 'Public',
  imageId: null,
  gender: null,
  maintainedByYou: true,
};

describe('DeletePerson', () => {
  let fixture: ComponentFixture<DeletePerson>;
  let deleted: string[];
  let refuse: boolean;
  let results: boolean[];

  beforeEach(async () => {
    deleted = [];
    refuse = false;
    results = [];
    TestBed.configureTestingModule({
      providers: [
        {
          provide: PeopleService,
          useValue: {
            delete: (id: string) => {
              if (refuse) {
                return Promise.reject(
                  new HttpErrorResponse({
                    status: 403,
                    error: { code: 'person.not_maintainer', title: 'Not permitted.' },
                  }),
                );
              }
              deleted.push(id);
              return Promise.resolve();
            },
          },
        },
      ],
    });
    fixture = TestBed.createComponent(DeletePerson);
    fixture.componentRef.setInput('person', gita);
    fixture.componentInstance.closed.subscribe((result) => results.push(result));
    await fixture.whenStable();
  });

  const button = (label: string) =>
    [...fixture.nativeElement.querySelectorAll('button')].find(
      (b: HTMLButtonElement) => b.textContent?.trim() === label,
    ) as HTMLButtonElement;

  it('names the person and says it cannot be undone (AC-08.14)', () => {
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Delete Gita Rai?');
    expect(text).toContain('This cannot be undone.');
  });

  it('deletes only after confirming (AC-08.14)', async () => {
    expect(deleted).toEqual([]);

    button('Delete').click();
    await fixture.whenStable();

    expect(deleted).toEqual(['gita']);
    expect(results).toEqual([true]);
  });

  it('deletes nothing when cancelled', async () => {
    button('Cancel').click();
    await fixture.whenStable();

    expect(deleted).toEqual([]);
    expect(results).toEqual([false]);
  });

  it('shows a refusal and stays open', async () => {
    refuse = true;

    button('Delete').click();
    await fixture.whenStable();

    expect(fixture.nativeElement.querySelector('[role="alert"]')).not.toBeNull();
    expect(results).toEqual([]);
  });
});
