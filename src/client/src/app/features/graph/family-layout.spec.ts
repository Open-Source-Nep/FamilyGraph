import { LayoutLink, LayoutNode, Point, familyLayout } from './family-layout';

const options = { height: 10, nodeSep: 8, groupSep: 24, rankSep: 30 };
const width = 10;
const link = (parentId: string, childId: string): LayoutLink => ({ parentId, childId });
const nodes = (ids: string[]): LayoutNode[] => ids.map((id) => ({ id, width }));

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
  const positions = familyLayout(nodes(ids), links, options);

  it('always puts children below each of their parents', () => {
    for (const { parentId, childId } of links) {
      expect(positions.get(childId)!.y - positions.get(parentId)!.y).toBeGreaterThanOrEqual(
        options.height + options.rankSep / 2 - 1e-9,
      );
    }
  });

  it('keeps children near their parents', () => {
    const row = options.height + options.rankSep;
    const near = (children: string[], parents: string[]) =>
      expect(Math.abs(middle(positions, children) - middle(positions, parents))).toBeLessThan(row);
    near(['c1', 'c2', 'c3'], ['asha', 'sam']);
    near(['c4'], ['bina', 'ravi']);
    near(['asha', 'bina'], ['hari', 'gita']);
    near(['ravi'], ['kiran', 'lata']);
  });

  it('never overlaps people', () => {
    for (const a of ids) {
      for (const b of ids) {
        if (a < b) {
          const pa = positions.get(a)!;
          const pb = positions.get(b)!;
          const apart = Math.abs(pa.x - pb.x) >= width || Math.abs(pa.y - pb.y) >= options.height;
          expect(apart, `${a} and ${b}`).toBe(true);
        }
      }
    }
  });

  it('draws the same family the same way every time', () => {
    expect(familyLayout(nodes(ids), links, options)).toEqual(positions);
  });

  it('places people with no links and ignores links to people not given', () => {
    const lone = familyLayout(nodes(['a', 'b']), [link('a', 'missing')], options);
    const [a, b] = [lone.get('a')!, lone.get('b')!];
    expect(Math.abs(a.x - b.x) >= width || Math.abs(a.y - b.y) >= options.height).toBe(true);
  });

  it('lays out the most people the home page shows quickly', () => {
    // Ten families of fifty: two founders, then couples with three children each.
    const many: string[] = [];
    const manyLinks: LayoutLink[] = [];
    for (let family = 0; family < 10; family++) {
      const people = Array.from({ length: 50 }, (_, i) => `f${family}p${i}`);
      many.push(...people);
      for (let i = 2; i < people.length; i++) {
        const couple = Math.floor((i - 2) / 3) * 2;
        manyLinks.push(link(people[couple], people[i]), link(people[couple + 1], people[i]));
      }
    }
    const started = performance.now();
    const laidOut = familyLayout(nodes(many), manyLinks, options);
    expect(performance.now() - started).toBeLessThan(3000);
    expect(laidOut.size).toBe(500);
  });
});
