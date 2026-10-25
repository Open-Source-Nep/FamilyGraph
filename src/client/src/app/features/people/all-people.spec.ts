import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { AllPeople, searchDelayMs } from './all-people';
import { PeopleService } from './people.service';
import { Person } from './person';

const person = (id: string, firstName: string, extra: Partial<Person> = {}): Person => ({
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
});

const asha = person('p1', 'Asha', { maintainedByYou: true, visibility: 'Private' });
const bina = person('p2', 'Bina', { description: 'Teacher' });
const gita = person('p3', 'Gita');

describe('AllPeople', () => {
  let fixture: ComponentFixture<AllPeople>;
  let browse: ReturnType<typeof vi.fn>;

  const names = () =>
    [...fixture.nativeElement.querySelectorAll('.person-rows .person-name')].map(
      (name: HTMLElement) => name.textContent?.replace(/\s+/g, ' ').trim(),
    );
  const showMore = () =>
    [...fixture.nativeElement.querySelectorAll('button')].find(
      (button: HTMLButtonElement) => button.textContent?.trim() === 'Show more',
    ) as HTMLButtonElement | undefined;

  beforeEach(async () => {
    // Two people a page, the way the server pages everyone by name.
    browse = vi.fn((text: string, offset: number) => {
      const words = text.toLowerCase().split(/\s+/).filter(Boolean);
      const matching = [asha, bina, gita].filter((p) =>
        words.every((word) =>
          [p.firstName, p.description].some((part) => part?.toLowerCase().includes(word)),
        ),
      );
      return Promise.resolve({
        people: matching.slice(offset, offset + 2),
        hasMore: matching.length > offset + 2,
      });
    });
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: PeopleService, useValue: { browse } }],
    });
    fixture = TestBed.createComponent(AllPeople);
    await fixture.whenStable();
  });

  // AC-08.18: everyone you may see, A to Z, a page at a time with Show more.
  it('lists the first page and adds the next one with Show more', async () => {
    expect(browse).toHaveBeenCalledWith('', 0);
    expect(names()).toEqual(['Asha (yours) (private)', 'Bina']);
    const links = [...fixture.nativeElement.querySelectorAll('.person-rows a')].map(
      (link: HTMLAnchorElement) => link.getAttribute('href'),
    );
    expect(links).toEqual(['/people/p1', '/people/p2']);
    expect(fixture.nativeElement.querySelector('.person-description')?.textContent).toBe('Teacher');

    showMore()!.click();
    await fixture.whenStable();

    expect(browse).toHaveBeenLastCalledWith('', 2);
    expect(names()).toEqual(['Asha (yours) (private)', 'Bina', 'Gita']);
    expect(showMore()).toBeUndefined();
  });

  // AC-08.20: the server is asked again with the typed words once typing pauses.
  it('searches on the server once typing pauses', async () => {
    const search = fixture.nativeElement.querySelector('input[type=search]') as HTMLInputElement;

    search.value = 'teach';
    search.dispatchEvent(new Event('input'));
    search.value = 'teacher';
    search.dispatchEvent(new Event('input'));
    await new Promise((resolve) => setTimeout(resolve, searchDelayMs + 50));
    await fixture.whenStable();

    expect(browse).toHaveBeenCalledTimes(2);
    expect(browse).toHaveBeenLastCalledWith('teacher', 0);
    expect(names()).toEqual(['Bina']);

    search.value = 'nobody';
    search.dispatchEvent(new Event('input'));
    await new Promise((resolve) => setTimeout(resolve, searchDelayMs + 50));
    await fixture.whenStable();

    expect(names()).toEqual([]);
    expect(fixture.nativeElement.textContent).toContain('Nobody matches.');
  });
});
