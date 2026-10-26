import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { Bansawali, BansawaliLine } from './bansawali';
import { BansawaliPage } from './bansawali-page';
import { GraphPerson } from './graph';
import { GraphService } from './graph.service';

const person = (id: string, gender: GraphPerson['gender'], lastName: string | null = null) =>
  ({
    id,
    firstName: id,
    middleName: null,
    lastName,
    placeholder: null,
    visibility: 'Public',
    imageId: null,
    gender,
    maintainedByYou: true,
    description: null,
    birthSortDate: null,
  }) as GraphPerson;

describe('BansawaliPage', () => {
  let fixture: ComponentFixture<BansawaliPage>;
  let requested: [string, BansawaliLine][];
  const bansawali: Bansawali = {
    rootId: 'Hari',
    line: 'Sons',
    people: [
      { person: person('Hari', 'Male', 'Neupane'), generation: 0, continues: true, hasMore: false },
      { person: person('Ram', 'Male'), generation: 1, continues: true, hasMore: true },
      { person: person('Sita', 'Female'), generation: 1, continues: false, hasMore: false },
    ],
    links: [
      { id: 'l2', parentId: 'Hari', childId: 'Sita', type: 'Biological', birthOrder: 2 },
      { id: 'l1', parentId: 'Hari', childId: 'Ram', type: 'Biological', birthOrder: 1 },
    ],
    spouses: [person('Gauri', 'Female')],
    marriages: [{ id: 'm1', firstSpouseId: 'Hari', secondSpouseId: 'Gauri', isFormer: false }],
    isTruncated: true,
  };

  beforeEach(() => {
    requested = [];
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: GraphService,
          useValue: {
            bansawali: (id: string, line: BansawaliLine) => {
              requested.push([id, line]);
              return Promise.resolve(bansawali);
            },
          },
        },
      ],
    });
  });

  const render = async (line?: string) => {
    fixture = TestBed.createComponent(BansawaliPage);
    fixture.componentRef.setInput('id', 'Hari');
    fixture.componentRef.setInput('line', line);
    await fixture.whenStable();
    fixture.detectChanges();
  };
  const text = (selector: string) =>
    [...fixture.nativeElement.querySelectorAll(selector)].map((el: HTMLElement) =>
      el.textContent!.replace(/\s+/g, ' ').trim(),
    );

  const parts = (selector: string) =>
    [...fixture.nativeElement.querySelectorAll(selector)].map((el: HTMLElement) =>
      [...el.children].map((child) => child.textContent!.trim()),
    );

  it('shows the outline through sons in birth order with spouses and a link to continue (AC-08.21, AC-08.23)', async () => {
    await render();

    expect(requested).toEqual([['Hari', 'Sons']]);
    expect(text('h1')).toEqual(['Neupane Bansawali']);
    expect(parts('.lineage-person')).toEqual([
      ['1', 'HN', 'Hari Neupane', 'wife Gauri'],
      ['2', 'R', 'Ram', '(son)', 'More descendants', '(more descendants)'],
      ['2', 'S', 'Sita', '(daughter)'],
    ]);
    expect(fixture.nativeElement.textContent).toContain('stops at the most people it can show');
  });

  it('follows every child with the toggle in the URL (AC-08.21)', async () => {
    await render('AllChildren');

    expect(requested).toEqual([['Hari', 'AllChildren']]);
    expect(text('nav.tabs a.current')).toEqual(['Through every child']);
  });

  it('shows small photos or initials by default and changes their size before printing (AC-08.26)', async () => {
    bansawali.people[1].person = { ...person('Ram', 'Male'), imageId: 'img1' };
    await render();
    const avatars = () =>
      [...fixture.nativeElement.querySelectorAll('.lineage-avatar')] as HTMLElement[];
    const choose = (size: string) => {
      const select = fixture.nativeElement.querySelector('.photo-size select') as HTMLSelectElement;
      select.value = size;
      select.dispatchEvent(new Event('change'));
      fixture.detectChanges();
    };

    expect(avatars().map((avatar) => avatar.className)).toEqual([
      'avatar lineage-avatar small',
      'avatar lineage-avatar small',
      'avatar lineage-avatar small',
    ]);
    expect(avatars()[1].querySelector('img')!.getAttribute('src')).toBe(
      '/api/people/Ram/image?v=img1',
    );

    choose('Large');
    expect(avatars().every((avatar) => avatar.classList.contains('large'))).toBe(true);

    choose('None');
    expect(avatars()).toEqual([]);
    bansawali.people[1].person = person('Ram', 'Male');
  });

  it('prints from the browser (AC-08.25)', async () => {
    const print = vi.spyOn(window, 'print').mockImplementation(() => undefined);
    await render();

    (fixture.nativeElement.querySelector('.actions button') as HTMLButtonElement).click();

    expect(print).toHaveBeenCalled();
  });
});
