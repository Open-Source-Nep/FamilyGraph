import { GraphLink, GraphMarriage, GraphPerson, LocalView, LocalViewPerson } from './graph';
import { byGeneration, expand, fromView, generationLabel } from './shown-graph';

const person = (id: string): GraphPerson => ({
  id,
  firstName: id,
  middleName: null,
  lastName: null,
  placeholder: null,
  visibility: 'Public',
  imageId: null,
  gender: null,
  maintainedByYou: false,
  description: null,
  birthSortDate: null,
});
const row = (id: string, generation: number, hasMore = false): LocalViewPerson => ({
  person: person(id),
  generation,
  hasMore,
});
const link = (
  parentId: string,
  childId: string,
  type: GraphLink['type'] = 'Biological',
  birthOrder: number | null = null,
): GraphLink => ({
  id: `${parentId}-${childId}`,
  parentId,
  childId,
  type,
  birthOrder,
});
const view = (
  centreId: string,
  people: LocalViewPerson[],
  links: GraphLink[],
  marriages: GraphMarriage[] = [],
): LocalView => ({
  centreId,
  people,
  links,
  marriages,
  isTruncated: false,
});

describe('shown graph', () => {
  // Maya is the centre; her parent Gita has a parent the first view did not reach.
  const first = view(
    'maya',
    [row('maya', 0), row('gita', 1, true), row('rina', -1)],
    [link('gita', 'maya'), link('maya', 'rina', 'Adoptive')],
  );

  it('adds the people around an edge person at their generation (AC-08.5)', () => {
    const around = view(
      'gita',
      [row('gita', 0), row('sita', 1), row('maya', -1, true)],
      [link('sita', 'gita'), link('gita', 'maya')],
    );

    const { graph, added } = expand(fromView(first), around);

    expect(added).toBe(1);
    expect(graph.centreId).toBe('maya');
    expect(graph.people.map((r) => [r.person.id, r.generation])).toEqual([
      ['maya', 0],
      ['gita', 1],
      ['rina', -1],
      ['sita', 2],
    ]);
    expect(graph.links.map((l) => l.id)).toEqual(['gita-maya', 'maya-rina', 'sita-gita']);
  });

  it('keeps "has more" only while every view says so', () => {
    const around = view('gita', [row('gita', 0, true), row('maya', -1, true)], []);

    const { graph } = expand(fromView(first), around);

    // Maya had no relatives outside the first view, so the second view cannot bring one back.
    expect(graph.people.find((r) => r.person.id === 'maya')?.hasMore).toBe(false);
  });

  it('clears "has more" on an edge person whose view added nobody', () => {
    const around = view('gita', [row('gita', 0, true), row('maya', -1)], []);

    const { graph, added } = expand(fromView(first), around);

    expect(added).toBe(0);
    expect(graph.people.find((r) => r.person.id === 'gita')?.hasMore).toBe(false);
  });

  it('lists each generation, oldest first, with shown parents and children (AC-09.2)', () => {
    const groups = byGeneration(fromView(first));

    expect(groups.map((g) => g.generation)).toEqual([1, 0, -1]);
    const maya = groups[1].people[0];
    expect(maya.parents.map((p) => p.person.id)).toEqual(['gita']);
    expect(maya.children.map((c) => [c.person.id, c.link.type])).toEqual([['rina', 'Adoptive']]);
  });

  it('merges marriages and lists spouses beside each person (AC-08.9, AC-09.2)', () => {
    const marriage: GraphMarriage = {
      id: 'm1',
      firstSpouseId: 'maya',
      secondSpouseId: 'ram',
      isFormer: true,
    };
    const withSpouse = view('maya', [row('maya', 0), row('ram', 0)], [], [marriage]);

    const { graph } = expand(fromView(first), withSpouse);
    const again = expand(graph, withSpouse).graph;

    expect(again.marriages).toEqual([marriage]);
    const maya = byGeneration(again)
      .find((g) => g.generation === 0)!
      .people.find((entry) => entry.row.person.id === 'maya')!;
    expect(maya.spouses.map((s) => [s.person.id, s.marriage.isFormer])).toEqual([['ram', true]]);
  });

  it('names generations relative to the centre', () => {
    expect(generationLabel(2)).toBe('2 generations above');
    expect(generationLabel(0)).toBe('Same generation');
    expect(generationLabel(-1)).toBe('1 generation below');
  });

  it("lists brothers and sisters and each person's children in birth order (AC-02.17)", () => {
    const family = view(
      'hari',
      [row('hari', 0), row('zara', -1), row('asha', -1), row('bina', -1)],
      [
        link('hari', 'zara'),
        link('hari', 'asha', 'Biological', 2),
        link('hari', 'bina', 'Biological', 1),
      ],
    );

    const [parents, children] = byGeneration(fromView(family));

    expect(parents.people[0].children.map((child) => child.person.id)).toEqual([
      'bina',
      'asha',
      'zara',
    ]);
    expect(children.people.map((entry) => entry.row.person.id)).toEqual(['bina', 'asha', 'zara']);
  });
});
