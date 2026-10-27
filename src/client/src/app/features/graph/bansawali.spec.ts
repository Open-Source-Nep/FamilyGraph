import {
  Bansawali,
  BansawaliEntry,
  BansawaliPerson,
  bansawaliSections,
  bansawaliTitle,
  bansawaliTree,
} from './bansawali';
import { GraphPerson } from './graph';
import { Gender } from '../people/person';

const person = (
  id: string,
  gender: Gender | null,
  lastName: string | null = null,
): GraphPerson => ({
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
});
const row = (p: GraphPerson, generation: number, continues = true): BansawaliPerson => ({
  person: p,
  generation,
  continues,
  hasMore: false,
});

describe('bansawaliTree', () => {
  const hari = person('Hari', 'Male', 'Neupane');
  const ram = person('Ram', 'Male');
  const sita = person('Sita', 'Female');
  const krishna = person('Krishna', 'Male');
  const gauri = person('Gauri', 'Female');
  const maya = person('Maya', 'Female');
  const bansawali: Bansawali = {
    rootId: 'Hari',
    line: 'Sons',
    people: [row(hari, 0), row(sita, 1, false), row(krishna, 1, false), row(ram, 1)],
    links: [
      { id: 'l1', parentId: 'Hari', childId: 'Sita', type: 'Biological', birthOrder: 2 },
      { id: 'l2', parentId: 'Hari', childId: 'Krishna', type: 'Adoptive', birthOrder: null },
      { id: 'l3', parentId: 'Hari', childId: 'Ram', type: 'Biological', birthOrder: 1 },
    ],
    spouses: [gauri, maya],
    marriages: [
      { id: 'm1', firstSpouseId: 'Hari', secondSpouseId: 'Maya', isFormer: true },
      { id: 'm2', firstSpouseId: 'Gauri', secondSpouseId: 'Hari', isFormer: false },
    ],
    isTruncated: false,
  };

  it('puts children under their parent in birth order, adopted ones marked (AC-08.21, AC-08.22)', () => {
    const root = bansawaliTree(bansawali)!;

    expect(root.relation).toBeNull();
    expect(
      root.children.map((entry) => [entry.row.person.id, entry.relation, entry.adopted]),
    ).toEqual([
      ['Ram', 'son', false],
      ['Sita', 'daughter', false],
      ['Krishna', 'adopted son', true],
    ]);
  });

  it('lists spouses beside the person, current first and former ones marked (AC-08.23)', () => {
    expect(bansawaliTree(bansawali)!.spouses.map((s) => `${s.label} ${s.person.id}`)).toEqual([
      'wife Gauri',
      'former wife Maya',
    ]);
  });

  it('is null without the ancestor', () => {
    expect(bansawaliTree({ ...bansawali, people: [] })).toBeNull();
  });
});

describe('bansawaliTitle', () => {
  it("names the ancestor's last name, or the ancestor without one", () => {
    expect(bansawaliTitle(person('Hari', 'Male', 'Neupane'))).toBe('Neupane Bansawali');
    expect(bansawaliTitle(person('Hari', 'Male'))).toBe('Bansawali of Hari');
    expect(bansawaliTitle({ ...person('x', null, 'Neupane'), placeholder: 'Private' })).toBe(
      'Bansawali of Private',
    );
  });
});

describe('bansawaliSections', () => {
  // A line of `depth` generations from p0, with a daughter beside p1.
  const line = (depth: number): Bansawali => {
    const people = Array.from({ length: depth }, (_, g) => row(person(`p${g}`, 'Male'), g));
    const daughter = row(person('d1', 'Female'), 1, false);
    return {
      rootId: 'p0',
      line: 'Sons',
      people: [...people, daughter],
      links: [
        ...people.slice(1).map((child, g) => ({
          id: `l${g}`,
          parentId: `p${g}`,
          childId: child.person.id,
          type: 'Biological' as const,
          birthOrder: 1,
        })),
        { id: 'ld', parentId: 'p0', childId: 'd1', type: 'Biological', birthOrder: 2 },
      ],
      spouses: [],
      marriages: [],
      isTruncated: false,
    };
  };
  // Generations shown from `entry` down, stopping at the people whose children continue elsewhere.
  const depthOf = (entry: BansawaliEntry, stops: Set<string>): number =>
    stops.has(entry.row.person.id)
      ? 1
      : 1 + Math.max(0, ...entry.children.map((child) => depthOf(child, stops)));

  it('keeps a Bansawali of up to eight generations in one section (AC-08.27)', () => {
    const { sections, continuedIn } = bansawaliSections(bansawaliTree(line(8))!);

    expect(sections.map((section) => section.number)).toEqual([1]);
    expect(continuedIn.size).toBe(0);
  });

  it('continues a 32-generation line in a new section every eight generations (AC-08.27)', () => {
    const { sections, continuedIn } = bansawaliSections(bansawaliTree(line(32))!);

    expect(
      sections.map((section) => [section.number, section.entry.row.person.id, section.from]),
    ).toEqual([
      [1, 'p0', null],
      [2, 'p7', 1],
      [3, 'p14', 2],
      [4, 'p21', 3],
      [5, 'p28', 4],
    ]);
    expect([...continuedIn]).toEqual([
      ['p7', 2],
      ['p14', 3],
      ['p21', 4],
      ['p28', 5],
    ]);
    for (const section of sections) {
      const stops = new Set(continuedIn.keys());
      stops.delete(section.entry.row.person.id);
      expect(depthOf(section.entry, stops)).toBeLessThanOrEqual(8);
    }
  });

  it('starts no section for someone without children in the Bansawali (AC-08.27)', () => {
    const bansawali = line(9);
    bansawali.people = bansawali.people.filter((r) => r.person.id !== 'p8');
    bansawali.links = bansawali.links.filter((l) => l.childId !== 'p8');

    expect(bansawaliSections(bansawaliTree(bansawali)!).sections).toHaveLength(1);
  });
});
