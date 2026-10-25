import { fullName } from '../people/person';
import { GraphLink, GraphPerson, LocalViewPerson } from './graph';

/** Nulls last, then the smaller first. */
function nullsLast<T>(a: T | null, b: T | null, compare: (a: T, b: T) => number): number {
  if (a === null || b === null) {
    return a === b ? 0 : a === null ? 1 : -1;
  }
  return compare(a, b);
}

/** Shown birth date, then name with hidden people last, then ID: the order after birth order. */
function compareUnordered(a: GraphPerson, b: GraphPerson): number {
  return (
    nullsLast(a.birthSortDate, b.birthSortDate, (x, y) => (x < y ? -1 : x > y ? 1 : 0)) ||
    Number(Boolean(a.placeholder)) - Number(Boolean(b.placeholder)) ||
    (a.placeholder ? 0 : fullName(a).localeCompare(fullName(b))) ||
    (a.id < b.id ? -1 : a.id > b.id ? 1 : 0)
  );
}

/**
 * One parent's children in their order (PRD 02, D-039): birth order first, then the unordered
 * ones by birth dates shown to others, then by name, with hidden people last by ID as on the
 * person page (D-025).
 */
export function compareChildren(
  a: { person: GraphPerson; link: GraphLink },
  b: { person: GraphPerson; link: GraphLink },
): number {
  return (
    nullsLast(a.link.birthOrder, b.link.birthOrder, (x, y) => x - y) ||
    compareUnordered(a.person, b.person)
  );
}

/**
 * Each set of shown brothers and sisters (the same shown parents) in their order, keyed by their
 * parents' IDs. Two siblings compare by their birth order under each shared parent in turn, then
 * as unordered children. People without shown parents are in no set.
 */
export function siblingSets(
  people: LocalViewPerson[],
  links: GraphLink[],
): Map<string, LocalViewPerson[]> {
  const shown = new Set(people.map((row) => row.person.id));
  const parentLinks = new Map<string, GraphLink[]>();
  for (const link of links) {
    if (shown.has(link.parentId) && shown.has(link.childId)) {
      parentLinks.set(link.childId, [...(parentLinks.get(link.childId) ?? []), link]);
    }
  }
  const family = (row: LocalViewPerson) =>
    (parentLinks.get(row.person.id) ?? [])
      .map((link) => link.parentId)
      .sort()
      .join(' ');
  const orderUnder = (row: LocalViewPerson, parentId: string) =>
    parentLinks.get(row.person.id)?.find((link) => link.parentId === parentId)?.birthOrder ?? null;
  const compare = (a: LocalViewPerson, b: LocalViewPerson) => {
    for (const parentId of family(a).split(' ')) {
      const byParent = nullsLast(orderUnder(a, parentId), orderUnder(b, parentId), (x, y) => x - y);
      if (byParent) {
        return byParent;
      }
    }
    return compareUnordered(a.person, b.person);
  };

  const sets = new Map<string, LocalViewPerson[]>();
  for (const row of people) {
    const key = family(row);
    if (key) {
      sets.set(key, [...(sets.get(key) ?? []), row]);
    }
  }
  return new Map([...sets].map(([key, rows]) => [key, rows.sort(compare)]));
}

/**
 * The shown people with each set of brothers and sisters in their order, for the layouts that
 * place people in the order given. Each set keeps the places its members had, so families stay
 * where they were; only who stands where within a set changes.
 */
export function orderSiblings(people: LocalViewPerson[], links: GraphLink[]): LocalViewPerson[] {
  const sets = siblingSets(people, links);
  const keyOf = new Map(
    [...sets].flatMap(([key, rows]) => rows.map((row) => [row.person.id, key] as const)),
  );
  const next = new Map<string, number>();
  return people.map((row) => {
    const key = keyOf.get(row.person.id);
    if (!key) {
      return row;
    }
    const index = next.get(key) ?? 0;
    next.set(key, index + 1);
    return sets.get(key)![index];
  });
}

/** A box on one row of the tree: its centre and width. */
export interface PlacedBox {
  id: string;
  x: number;
  width: number;
}

/**
 * New centres for one row of the tree, so that each set of brothers and sisters reads eldest on
 * the left (D-039). dagre orders a row to reduce crossings and may mix up siblings. Each sibling
 * comes with the spouses standing right next to them; when those groups stand side by side, they
 * are laid out again in birth order over the same stretch, keeping the gaps. Siblings with someone
 * else between them are left where dagre put them.
 */
export function orderRow(
  row: PlacedBox[],
  sets: string[][],
  partners: Map<string, Set<string>>,
): Map<string, number> {
  const boxes = [...row].sort((a, b) => a.x - b.x);
  const moved = new Map<string, number>();
  for (const set of sets) {
    const members = set.filter((id) => boxes.some((box) => box.id === id));
    if (members.length < 2) {
      continue;
    }
    const inSet = new Set(members);
    const index = new Map(boxes.map((box, position) => [box.id, position]));
    const units = members.map((id) => {
      let start = index.get(id)!;
      let end = start;
      const unit = new Set([id]);
      const attached = (box: PlacedBox | undefined) =>
        box !== undefined &&
        !inSet.has(box.id) &&
        [...unit].some((other) => partners.get(other)?.has(box.id));
      while (attached(boxes[start - 1])) {
        unit.add(boxes[--start].id);
      }
      while (attached(boxes[end + 1])) {
        unit.add(boxes[++end].id);
      }
      return { start, end };
    });
    const byPlace = [...units].sort((a, b) => a.start - b.start);
    if (byPlace.some((unit, k) => k > 0 && unit.start !== byPlace[k - 1].end + 1)) {
      continue;
    }
    const left = (box: PlacedBox) => box.x - box.width / 2;
    const right = (box: PlacedBox) => box.x + box.width / 2;
    // The gaps between the groups as they stood, reused in the same places.
    const gaps = byPlace
      .slice(1)
      .map((unit, k) => left(boxes[unit.start]) - right(boxes[byPlace[k].end]));
    let cursor = left(boxes[byPlace[0].start]);
    units.forEach((unit, k) => {
      const offset = cursor - left(boxes[unit.start]);
      for (let position = unit.start; position <= unit.end; position++) {
        moved.set(boxes[position].id, boxes[position].x + offset);
      }
      cursor = right(boxes[unit.end]) + offset + (gaps[k] ?? 0);
    });
  }
  return moved;
}
