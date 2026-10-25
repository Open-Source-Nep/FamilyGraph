import { Bansawali, BansawaliPerson, bansawaliTitle, bansawaliTree } from './bansawali';
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
