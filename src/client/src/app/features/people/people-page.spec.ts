import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { PeoplePage } from './people-page';
import { PeopleService } from './people.service';
import { PersonSummary } from './person';

const profile = (id: string, firstName: string, extra: Partial<PersonSummary> = {}) => ({
  id,
  firstName,
  middleName: null,
  lastName: null,
  visibility: 'Public' as const,
  gender: null,
  imageId: null,
  description: null,
  ...extra,
});

describe('PeoplePage', () => {
  let fixture: ComponentFixture<PeoplePage>;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: PeopleService,
          useValue: {
            listMaintained: () =>
              Promise.resolve({
                people: [
                  profile('p1', 'Maya', { lastName: 'Rai', imageId: 'i1', description: 'Teacher' }),
                  profile('p2', 'Hari', { visibility: 'Private' }),
                ],
                isTruncated: false,
              }),
          },
        },
      ],
    });
    fixture = TestBed.createComponent(PeoplePage);
    await fixture.whenStable();
  });

  // AC-01.33: each row has the photo or initials, the name and the description.
  it('lists each profile with its avatar and description', () => {
    const rows = [
      ...fixture.nativeElement.querySelectorAll('.person-rows a'),
    ] as HTMLAnchorElement[];

    expect(rows.map((row) => row.getAttribute('href'))).toEqual(['/people/p1', '/people/p2']);
    expect(rows[0].querySelector('img')?.getAttribute('src')).toBe('/api/people/p1/image?v=i1');
    expect(rows[0].querySelector('.person-description')?.textContent).toBe('Teacher');
    expect(rows[1].querySelector('.avatar')?.textContent?.trim()).toBe('H');
    expect(rows[1].querySelector('.person-name')?.textContent?.replace(/\s+/g, ' ').trim()).toBe(
      'Hari (private)',
    );
    expect(rows[1].querySelector('.person-description')).toBeNull();
  });

  it('filters the list by name or description as you type', async () => {
    const search = fixture.nativeElement.querySelector('input[type=search]') as HTMLInputElement;
    const names = () =>
      [...fixture.nativeElement.querySelectorAll('.person-rows .person-name')].map(
        (name: HTMLElement) => name.textContent?.replace(/\s+/g, ' ').trim(),
      );

    search.value = 'teach RAI';
    search.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    expect(names()).toEqual(['Maya Rai']);

    search.value = 'gurung';
    search.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    expect(names()).toEqual([]);
    expect(fixture.nativeElement.textContent).toContain('Nobody matches.');

    search.value = ' ';
    search.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    expect(names()).toEqual(['Maya Rai', 'Hari (private)']);
  });
});
