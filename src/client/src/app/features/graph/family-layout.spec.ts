import { LayoutLink, Point, familyLayout } from './family-layout';

const options = { height: 10, nodeSep: 8, groupSep: 24, rankSep: 30 };
const width = 10;
const link = (parentId: string, childId: string): LayoutLink => ({ parentId, childId });

/** The people on the row at `y`, left to right. */
function row(positions: Map<string, Point>, y: number): string[] {
  return [...positions.entries()]
    .filter(([, point]) => point.y === y)
    .sort(([, a], [, b]) => a.x - b.x)
    .map(([id]) => id);
}

const middle = (positions: Map<string, Point>, ids: string[]) =>
  ids.reduce((sum, id) => sum + positions.get(id)!.x, 0) / ids.length;

describe('family layout', () => {
  // Grandparents Hari and Gita have Asha and Bina. Asha and Sam (no parents shown) have three
  // children; Bina and Ravi (whose parents are Kiran and Lata) have one. Listed out of order.
  const ids = [
    'c2',
    'kiran',
    'bina',
    'c4',
    'sam',
    'hari',
    'c1',
    'asha',
    'lata',
    'ravi',
    'gita',
    'c3',
  ];
  const links = [
    link('hari', 'asha'),
    link('gita', 'asha'),
    link('hari', 'bina'),
    link('gita', 'bina'),
    link('kiran', 'ravi'),
    link('lata', 'ravi'),
    ...['c1', 'c2', 'c3'].flatMap((child) => [link('asha', child), link('sam', child)]),
    link('bina', 'c4'),
    link('ravi', 'c4'),
  ];
  const positions = familyLayout(
    ids.map((id) => ({ id, width })),
    links,
    options,
  );
  const rowHeight = options.height + options.rankSep;
  const rows = [0, 1, 2].map((rank) => row(positions, rank * rowHeight));

  it('puts each generation on its own row, with partners on the same row', () => {
    expect(rows.map((ids) => ids.length)).toEqual([4, 4, 4]);
    expect(rows[0]).toEqual(expect.arrayContaining(['hari', 'gita', 'kiran', 'lata']));
    expect(rows[1]).toEqual(expect.arrayContaining(['asha', 'sam', 'bina', 'ravi']));
    expect(rows[2]).toEqual(expect.arrayContaining(['c1', 'c2', 'c3', 'c4']));
  });

  it('keeps partners next to each other and brothers and sisters together', () => {
    const adjacent = (ids: string[], a: string, b: string) =>
      Math.abs(ids.indexOf(a) - ids.indexOf(b)) === 1;
    expect(adjacent(rows[0], 'hari', 'gita')).toBe(true);
    expect(adjacent(rows[0], 'kiran', 'lata')).toBe(true);
    expect(adjacent(rows[1], 'asha', 'sam')).toBe(true);
    expect(adjacent(rows[1], 'bina', 'ravi')).toBe(true);
    const siblings = ['c1', 'c2', 'c3'].map((id) => rows[2].indexOf(id)).sort();
    expect(siblings[2] - siblings[0]).toBe(2);
  });

  it('places children right below their parents', () => {
    // Within the span of the parents, once rows are crowded; centred when there is room.
    const below = (children: string[], parents: string[]) => {
      const xs = parents.map((id) => positions.get(id)!.x);
      const x = middle(positions, children);
      expect(x).toBeGreaterThanOrEqual(Math.min(...xs) - 1e-9);
      expect(x).toBeLessThanOrEqual(Math.max(...xs) + 1e-9);
    };
    below(['c1', 'c2', 'c3'], ['asha', 'sam']);
    below(['c4'], ['bina', 'ravi']);
    below(['asha', 'bina'], ['hari', 'gita']);
    below(['ravi'], ['kiran', 'lata']);
  });

  it('never overlaps people and leaves more room between families', () => {
    for (const ids of rows) {
      for (let i = 1; i < ids.length; i++) {
        const gap = positions.get(ids[i])!.x - positions.get(ids[i - 1])!.x - width;
        expect(gap).toBeGreaterThanOrEqual(options.nodeSep - 1e-9);
      }
    }
    const between = (a: string, b: string) =>
      Math.abs(positions.get(a)!.x - positions.get(b)!.x) - width;
    expect(
      between('c3', 'c4') >= options.groupSep - 1e-9 ||
        between('c1', 'c4') >= options.groupSep - 1e-9,
    ).toBe(true);
  });

  it('places people with no links and ignores links to people not given', () => {
    const lone = familyLayout(
      [
        { id: 'a', width },
        { id: 'b', width },
      ],
      [link('a', 'missing')],
      options,
    );
    expect(lone.get('a')!.y).toBe(0);
    expect(lone.get('b')!.y).toBe(0);
    expect(Math.abs(lone.get('a')!.x - lone.get('b')!.x)).toBeGreaterThanOrEqual(width);
  });
});
