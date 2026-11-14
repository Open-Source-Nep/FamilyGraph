import { childLines, trunkLevels } from './couple-lines';
import { GraphLink, GraphMarriage } from './graph';

const link = (
  parentId: string,
  childId: string,
  type: GraphLink['type'] = 'Biological',
): GraphLink => ({ id: `${parentId}-${childId}`, parentId, childId, type, birthOrder: null });
const marriage = (a: string, b: string, isFormer = false): GraphMarriage => ({
  id: `${a}+${b}`,
  firstSpouseId: a,
  secondSpouseId: b,
  isFormer,
});
const ids = (...people: string[]) => new Set(people);

// AC-08.35: one line per child from the middle of the married parents' line.
describe('childLines', () => {
  it('joins the two links of each child of a married couple', () => {
    const lines = childLines(
      ids('hari', 'gita', 'ram', 'sita'),
      [link('hari', 'ram'), link('gita', 'ram'), link('hari', 'sita'), link('gita', 'sita')],
      [marriage('hari', 'gita')],
    );

    expect(lines.single).toEqual([]);
    expect(lines.couples.map((line) => [line.marriageId, line.childId])).toEqual([
      ['hari+gita', 'ram'],
      ['hari+gita', 'sita'],
    ]);
    expect(lines.couples[0].links.map((joined) => joined.id)).toEqual(['hari-ram', 'gita-ram']);
  });

  it('keeps a line from each parent when they are not married to each other', () => {
    const links = [link('hari', 'ram'), link('gita', 'ram')];

    expect(childLines(ids('hari', 'gita', 'ram'), links, [])).toEqual({
      couples: [],
      single: links,
    });
  });

  it('keeps the line of a child of only one of the spouses', () => {
    const lines = childLines(
      ids('hari', 'gita', 'ram', 'maya'),
      [link('hari', 'ram'), link('gita', 'ram'), link('hari', 'maya')],
      [marriage('hari', 'gita')],
    );

    expect(lines.couples.map((line) => line.childId)).toEqual(['ram']);
    expect(lines.single.map((single) => single.id)).toEqual(['hari-maya']);
  });

  it('keeps both lines when the marriage ended', () => {
    const links = [link('hari', 'ram'), link('gita', 'ram')];

    expect(
      childLines(ids('hari', 'gita', 'ram'), links, [marriage('hari', 'gita', true)]).single,
    ).toEqual(links);
  });

  // AC-08.3: an adoptive link stays told apart from a biological one.
  it('keeps both lines when one parent is biological and the other adoptive', () => {
    const links = [link('hari', 'ram'), link('gita', 'ram', 'Adoptive')];

    expect(
      childLines(ids('hari', 'gita', 'ram'), links, [marriage('hari', 'gita')]).single,
    ).toEqual(links);
  });

  it('joins two adoptive links', () => {
    const lines = childLines(
      ids('hari', 'gita', 'ram'),
      [link('hari', 'ram', 'Adoptive'), link('gita', 'ram', 'Adoptive')],
      [marriage('hari', 'gita')],
    );

    expect(lines.couples.map((line) => line.links[0].type)).toEqual(['Adoptive']);
  });

  it('joins each married pair of a child with two sets of parents', () => {
    const lines = childLines(
      ids('hari', 'gita', 'kiran', 'lata', 'ram'),
      [
        link('hari', 'ram'),
        link('kiran', 'ram', 'Adoptive'),
        link('gita', 'ram'),
        link('lata', 'ram', 'Adoptive'),
      ],
      [marriage('hari', 'gita'), marriage('kiran', 'lata')],
    );

    expect(lines.single).toEqual([]);
    expect(lines.couples.map((line) => line.marriageId)).toEqual(['hari+gita', 'kiran+lata']);
  });

  it('leaves out links and marriages of people not shown', () => {
    const lines = childLines(
      ids('hari', 'ram'),
      [link('hari', 'ram'), link('gita', 'ram'), link('hari', 'far')],
      [marriage('hari', 'gita')],
    );

    expect(lines.couples).toEqual([]);
    expect(lines.single.map((single) => single.id)).toEqual(['hari-ram']);
  });
});

// AC-08.35: bars of neighbouring couples never run over each other.
describe('trunkLevels', () => {
  it('keeps bars that do not meet at one height', () => {
    const levels = trunkLevels(
      [
        { id: 'b', from: 120, to: 200 },
        { id: 'a', from: 0, to: 100 },
      ],
      8,
    );

    expect([levels.get('a'), levels.get('b')]).toEqual([0, 0]);
  });

  it('puts bars that would overlap, or nearly touch, at different heights', () => {
    const levels = trunkLevels(
      [
        { id: 'a', from: 0, to: 100 },
        { id: 'b', from: 50, to: 200 },
        { id: 'c', from: 104, to: 150 },
        { id: 'd', from: 220, to: 300 },
      ],
      8,
    );

    expect(['a', 'b', 'c', 'd'].map((id) => levels.get(id))).toEqual([0, 1, 2, 0]);
  });
});
