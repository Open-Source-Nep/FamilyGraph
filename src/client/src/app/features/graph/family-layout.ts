/** A person to place, with their width on screen. */
export interface LayoutNode {
  id: string;
  width: number;
}

export interface LayoutLink {
  parentId: string;
  childId: string;
}

export interface LayoutOptions {
  /** Height of a person on screen. */
  height: number;
  /** Space between brothers and sisters and between partners. */
  nodeSep: number;
  /** Space between people of different families on the same row. */
  groupSep: number;
  /** Space between generations. */
  rankSep: number;
}

export interface Point {
  x: number;
  y: number;
}

/** Down, up, down, up, down: the last pass puts children under their parents. */
const passes = 5;

/**
 * Places people in rows by generation with each family grouped together: brothers and sisters
 * side by side directly below their parents, partners (the other parents of someone's children)
 * next to each other, and more space between families than within one. Rows are not spread out
 * evenly, so a large family takes the room it needs and a small one stays compact (D-024).
 *
 * A person's row is below all their parents and one above their highest child, so partners share a
 * row even when only one of them has ancestors shown. The order within a row follows the parents' order,
 * and positions alternate between centring children under their parents and parents over their
 * children.
 */
export function familyLayout(
  nodes: LayoutNode[],
  links: LayoutLink[],
  options: LayoutOptions,
): Map<string, Point> {
  const index = new Map(nodes.map((node, i) => [node.id, i]));
  const width = new Map(nodes.map((node) => [node.id, node.width]));
  const parents = new Map<string, string[]>(nodes.map((node) => [node.id, []]));
  const children = new Map<string, string[]>(nodes.map((node) => [node.id, []]));
  for (const link of links) {
    if (index.has(link.parentId) && index.has(link.childId) && link.parentId !== link.childId) {
      parents.get(link.childId)!.push(link.parentId);
      children.get(link.parentId)!.push(link.childId);
    }
  }

  const rank = ranks(nodes, parents, children);
  const rows = new Map<number, string[]>();
  for (const node of nodes) {
    rows.set(rank.get(node.id)!, [...(rows.get(rank.get(node.id)!) ?? []), node.id]);
  }
  const rowRanks = [...rows.keys()].sort((a, b) => a - b);

  // Partners share a child; only partners on the same row are kept next to each other.
  const partners = new Map<string, Set<string>>(nodes.map((node) => [node.id, new Set()]));
  for (const ids of parents.values()) {
    for (const a of ids) {
      for (const b of ids) {
        if (a !== b && rank.get(a) === rank.get(b)) {
          partners.get(a)!.add(b);
        }
      }
    }
  }

  const mean = (ids: string[], x: Map<string, number>): number | null => {
    const known = ids.filter((id) => x.has(id));
    return known.length ? known.reduce((sum, id) => sum + x.get(id)!, 0) / known.length : null;
  };

  let x = new Map<string, number>();
  const orders = new Map<number, string[]>();
  /** The first person of each person's couple, from the last down pass. */
  const couple = new Map<string, string>();
  /** Brothers and sisters share their parents, so they share this key. */
  const family = new Map(
    nodes.map((node) => [node.id, [...parents.get(node.id)!].sort().join(' ')]),
  );
  for (let pass = 0; pass < passes; pass++) {
    if (pass % 2 === 1) {
      // Up: parents over the middle of their children, keeping each row's order.
      for (const r of [...rowRanks].reverse()) {
        const order = orders.get(r)!;
        const desired = order.map((id) => mean(children.get(id)!, x) ?? x.get(id)!);
        place(order, desired, x);
      }
      continue;
    }

    // Down: each row ordered and placed under the row above.
    const previous = x;
    x = new Map();
    for (const r of rowRanks) {
      const ownParents = (id: string) => mean(parents.get(id)!, x);
      // Partners, and their other partners, are kept side by side as one couple.
      const visited = new Set<string>();
      const couples: {
        ids: string[];
        key: number;
        order: number;
        family: string;
        first: number;
      }[] = [];
      for (const seed of rows.get(r)!) {
        if (visited.has(seed)) {
          continue;
        }
        const ids = [seed];
        visited.add(seed);
        for (let i = 0; i < ids.length; i++) {
          for (const partner of partners.get(ids[i])!) {
            if (!visited.has(partner)) {
              visited.add(partner);
              ids.push(partner);
            }
          }
        }
        // Under the middle of all their parents; with none shown, where the last pass put them.
        const above = mean(
          ids.flatMap((id) => parents.get(id)!),
          x,
        );
        const below = mean(
          ids.flatMap((id) => children.get(id)!),
          previous,
        );
        const key = above ?? below ?? mean(ids, previous) ?? Number.POSITIVE_INFINITY;
        // The order also leans towards their children's side, so fewer lines cross.
        const order = above !== null && below !== null ? (above + below) / 2 : key;
        // Whoever's parents are further left goes first.
        const side = (id: string) => ownParents(id) ?? key;
        ids.sort((a, b) => side(a) - side(b) || index.get(a)! - index.get(b)!);
        const child = ids.find((id) => ownParents(id) !== null);
        for (const id of ids) {
          couple.set(id, seed);
        }
        couples.push({
          ids,
          key,
          order,
          family: child ? family.get(child)! : `#${seed}`,
          first: Math.min(...ids.map((id) => index.get(id)!)),
        });
      }
      // Brothers and sisters stay together even when two families' parents are in the same place.
      const familyFirst = new Map<string, number>();
      for (const c of couples) {
        familyFirst.set(c.family, Math.min(familyFirst.get(c.family) ?? c.first, c.first));
      }
      couples.sort(
        (a, b) =>
          a.order - b.order ||
          familyFirst.get(a.family)! - familyFirst.get(b.family)! ||
          a.first - b.first,
      );

      const order = couples.flatMap((c) => c.ids);
      const desired = couples.flatMap((c) => c.ids.map(() => c.key));
      orders.set(r, order);
      place(order, desired, x);
    }
  }

  return new Map(
    nodes.map((node) => [
      node.id,
      { x: x.get(node.id)!, y: rank.get(node.id)! * (options.height + options.rankSep) },
    ]),
  );

  /**
   * Sets the row's positions as close to the desired ones as the order and spacing allow (least
   * squares). Desired positions that are not finite follow the person before.
   */
  function place(order: string[], desired: number[], into: Map<string, number>): void {
    const offsets: number[] = [];
    for (let i = 0; i < order.length; i++) {
      if (i === 0) {
        offsets.push(0);
        continue;
      }
      const [a, b] = [order[i - 1], order[i]];
      const together =
        couple.get(a) === couple.get(b) ||
        (family.get(a) !== '' && family.get(a) === family.get(b));
      const sep = together ? options.nodeSep : options.groupSep;
      offsets.push(offsets[i - 1] + (width.get(a)! + width.get(b)!) / 2 + sep);
    }
    const targets: number[] = [];
    for (let i = 0; i < order.length; i++) {
      const target = desired[i] - offsets[i];
      targets.push(Number.isFinite(target) ? target : i === 0 ? 0 : targets[i - 1]);
    }
    const fitted = increasing(targets);
    order.forEach((id, i) => into.set(id, fitted[i] + offsets[i]));
  }
}

/** The non-decreasing sequence closest to `values` (pool adjacent violators). */
function increasing(values: number[]): number[] {
  const blocks: { sum: number; count: number }[] = [];
  for (const value of values) {
    blocks.push({ sum: value, count: 1 });
    while (blocks.length > 1) {
      const last = blocks[blocks.length - 1];
      const before = blocks[blocks.length - 2];
      if (before.sum / before.count <= last.sum / last.count) {
        break;
      }
      before.sum += last.sum;
      before.count += last.count;
      blocks.pop();
    }
  }
  return blocks.flatMap((block) => Array<number>(block.count).fill(block.sum / block.count));
}

/** Each person's row: below all their parents, and as close above their children as that allows. */
function ranks(
  nodes: LayoutNode[],
  parents: Map<string, string[]>,
  children: Map<string, string[]>,
): Map<string, number> {
  const rank = new Map<string, number>();
  const waiting = new Map(nodes.map((node) => [node.id, parents.get(node.id)!.length]));
  const ready = nodes.filter((node) => waiting.get(node.id) === 0).map((node) => node.id);
  for (const id of ready) {
    rank.set(id, 0);
  }
  while (ready.length) {
    const id = ready.shift()!;
    for (const child of children.get(id)!) {
      rank.set(child, Math.max(rank.get(child) ?? 0, rank.get(id)! + 1));
      waiting.set(child, waiting.get(child)! - 1);
      if (waiting.get(child) === 0) {
        ready.push(child);
      }
    }
  }
  // The cycle check keeps links acyclic; anyone left over would still get a row.
  for (const node of nodes) {
    if (!rank.has(node.id)) {
      rank.set(node.id, 0);
    }
  }
  // People move down to just above their highest child, deepest first, so someone whose
  // ancestors are not all shown sits next to the other parent of their children.
  for (const node of [...nodes].sort((a, b) => rank.get(b.id)! - rank.get(a.id)!)) {
    const own = children.get(node.id)!;
    if (own.length) {
      rank.set(node.id, Math.min(...own.map((child) => rank.get(child)!)) - 1);
    }
  }
  return rank;
}
