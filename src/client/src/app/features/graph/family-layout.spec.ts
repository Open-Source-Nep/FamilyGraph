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

  it('puts spouses on one row next to each other (D-031)', () => {
    // Ram's parents are shown and Sita's are not; they have no children together.
    const positions = familyLayout(
      nodes(['dasharath', 'ram', 'sita', 'lava']),
      [link('dasharath', 'ram'), link('sita', 'lava')],
      options,
      [{ spouseId: 'ram', otherSpouseId: 'sita' }],
    );
    const [ram, sita] = [positions.get('ram')!, positions.get('sita')!];
    expect(ram.y).toBe(sita.y);
    expect(Math.abs(ram.x - sita.x)).toBeCloseTo(width + options.nodeSep);
    expect(positions.get('lava')!.y).toBeGreaterThan(sita.y);
  });

  // Hari and Gita's son Ram married Maya, whose parents Kiran and Lata are shown too; Ram's brother
  // Shyam married Rita. Bikash married Anju and, before her, Puja, with a child from each.
  const families = [
    'hari',
    'gita',
    'kiran',
    'lata',
    'ram',
    'shyam',
    'bikash',
    'maya',
    'prakash',
    'rita',
    'anju',
    'puja',
    'aarav',
    'kabir',
    'nabin',
  ];
  const familyLinks = [
    ...['ram', 'shyam', 'bikash'].flatMap((child) => [link('hari', child), link('gita', child)]),
    ...['maya', 'prakash'].flatMap((child) => [link('kiran', child), link('lata', child)]),
    link('ram', 'aarav'),
    link('maya', 'aarav'),
    link('bikash', 'kabir'),
    link('anju', 'kabir'),
    link('bikash', 'nabin'),
    link('puja', 'nabin'),
  ];
  const familyMarriages = [
    ['hari', 'gita'],
    ['kiran', 'lata'],
    ['ram', 'maya'],
    ['shyam', 'rita'],
    ['bikash', 'anju'],
    ['bikash', 'puja'],
  ].map(([spouseId, otherSpouseId]) => ({ spouseId, otherSpouseId }));

  /** Nobody stands between two spouses on their row. */
  const expectSpousesTogether = (positions: Map<string, Point>) => {
    for (const { spouseId, otherSpouseId } of familyMarriages) {
      const [a, b] = [positions.get(spouseId)!, positions.get(otherSpouseId)!];
      expect(a.y, `${spouseId} and ${otherSpouseId}`).toBe(b.y);
      const between = families.filter((id) => {
        const p = positions.get(id)!;
        return p.y === a.y && p.x > Math.min(a.x, b.x) && p.x < Math.max(a.x, b.x);
      });
      expect(between, `between ${spouseId} and ${otherSpouseId}`).toEqual([]);
    }
  };

  it('keeps spouses side by side with nobody between them, settled or not (D-031)', () => {
    expectSpousesTogether(familyLayout(nodes(families), familyLinks, options, familyMarriages));
    expectSpousesTogether(
      familyLayout(nodes(families), familyLinks, { ...options, settle: false }, familyMarriages),
    );
  });

  it('puts someone married twice between their two spouses', () => {
    const positions = familyLayout(
      nodes(families),
      familyLinks,
      { ...options, settle: false },
      familyMarriages,
    );
    const x = (id: string) => positions.get(id)!.x;
    expect(Math.min(x('anju'), x('puja'))).toBeLessThan(x('bikash'));
    expect(Math.max(x('anju'), x('puja'))).toBeGreaterThan(x('bikash'));
  });

  it('keeps every generation on its row without settling, children on the row below', () => {
    const positions = familyLayout(
      nodes(families),
      familyLinks,
      { ...options, settle: false },
      familyMarriages,
    );
    const row = options.height + options.rankSep;
    for (const { parentId, childId } of familyLinks) {
      expect(positions.get(childId)!.y - positions.get(parentId)!.y).toBe(row);
    }
    // Each couple's children stand under them.
    const under = (children: string[], parents: string[]) =>
      expect(Math.abs(middle(positions, children) - middle(positions, parents))).toBeLessThan(row);
    under(['aarav'], ['ram', 'maya']);
    under(['maya', 'prakash'], ['kiran', 'lata']);
    under(['kabir'], ['bikash', 'anju']);
    under(['nabin'], ['bikash', 'puja']);
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
