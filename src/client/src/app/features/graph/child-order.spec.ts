import { compareChildren, orderRow, orderSiblings } from './child-order';
import { GraphLink, GraphPerson, LocalViewPerson } from './graph';

const person = (id: string, extra: Partial<GraphPerson> = {}): GraphPerson => ({
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
  ...extra,
});
const row = (p: GraphPerson, generation = -1): LocalViewPerson => ({
  person: p,
  generation,
  hasMore: false,
});
const link = (parentId: string, childId: string, birthOrder: number | null = null): GraphLink => ({
  id: `${parentId}-${childId}`,
  parentId,
  childId,
  type: 'Biological',
  birthOrder,
});

// AC-02.17: birth order, then birth dates shown to others, then name; hidden people last.
describe('compareChildren', () => {
  it('puts ordered children first, then shown birth dates, then names, then placeholders', () => {
    const children = [
      { person: person('Zara'), link: link('p', 'Zara') },
      {
        person: person('Hidden', { placeholder: 'Private', firstName: null }),
        link: link('p', 'h'),
      },
      { person: person('Bina', { birthSortDate: '1990-01-01' }), link: link('p', 'Bina') },
      { person: person('Asha'), link: link('p', 'Asha') },
      { person: person('Chandra', { birthSortDate: '1985-03-01' }), link: link('p', 'Chandra') },
      { person: person('Ganga'), link: link('p', 'Ganga', 2) },
      { person: person('Dipa'), link: link('p', 'Dipa', 1) },
    ];

    expect(children.sort(compareChildren).map((child) => child.person.id)).toEqual([
      'Dipa',
      'Ganga',
      'Chandra',
      'Bina',
      'Asha',
      'Zara',
      'Hidden',
    ]);
  });
});

describe('orderSiblings', () => {
  it('orders each set of brothers and sisters within the places they had', () => {
    const people = [
      row(person('mother'), 0),
      row(person('Zara')),
      row(person('other'), 0),
      row(person('Kiran')),
      row(person('Asha')),
      row(person('Bina')),
    ];
    const links = [
      link('mother', 'Zara'),
      link('mother', 'Asha', 2),
      link('mother', 'Bina', 1),
      link('other', 'Kiran'),
    ];

    expect(orderSiblings(people, links).map((shown) => shown.person.id)).toEqual([
      'mother',
      'Bina',
      'other',
      'Kiran',
      'Asha',
      'Zara',
    ]);
  });

  it('keeps half siblings in their own sets', () => {
    const people = [
      row(person('father'), 0),
      row(person('mother'), 0),
      row(person('Zara')),
      row(person('Asha')),
    ];
    // Zara has both parents, Asha only the father: different sets, so nothing moves.
    const links = [link('father', 'Zara'), link('mother', 'Zara'), link('father', 'Asha', 1)];

    expect(orderSiblings(people, links).map((shown) => shown.person.id)).toEqual([
      'father',
      'mother',
      'Zara',
      'Asha',
    ]);
  });
});

describe('orderRow', () => {
  const box = (id: string, left: number, width = 100) => ({ id, x: left + width / 2, width });

  it('lays siblings out eldest first over the same stretch, each with the spouse beside them', () => {
    // dagre put them as Zara, Asha + her husband Ram, Bina; the order is Bina, Asha, Zara.
    const row = [box('zara', 0), box('asha', 120, 80), box('ram', 210), box('bina', 330)];
    const partners = new Map([
      ['asha', new Set(['ram'])],
      ['ram', new Set(['asha'])],
    ]);

    const moved = orderRow(row, [['bina', 'asha', 'zara']], partners);

    expect(Object.fromEntries(moved)).toEqual({
      bina: 50,
      asha: 160,
      ram: 260,
      zara: 380,
    });
  });

  it('leaves siblings alone when someone else stands between them', () => {
    const row = [box('zara', 0), box('cousin', 120), box('asha', 240)];

    expect(orderRow(row, [['asha', 'zara']], new Map()).size).toBe(0);
  });
});
