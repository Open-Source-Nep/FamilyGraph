/** A person to place, with their width on screen. */
export interface LayoutNode {
  id: string;
  width: number;
}

export interface LayoutLink {
  parentId: string;
  childId: string;
}

/** A marriage: its two people share a row where their parents and children allow (D-031). */
export interface LayoutMarriage {
  spouseId: string;
  otherSpouseId: string;
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
  /**
   * Lets people settle off their rows (the home page's dots). Without it every generation stays
   * on its row, as the graph page's tree draws it.
   */
  settle?: boolean;
}

export interface Point {
  x: number;
  y: number;
}

/** Down, up, down, up, down: the last pass puts children under their parents. */
const passes = 5;

/**
 * Places a family so each one stays grouped, with children below their parents and nothing in
 * a fixed grid (D-024). It starts from rows by generation (brothers and sisters side by side under
 * their parents, partners next to each other, more space between families than within one), then
 * lets people settle off their rows to where the lines are shortest and cross least.
 *
 * A person's starting row is below all their parents and one above their highest child, so
 * partners share a row even when only one of them has ancestors shown. Spouses are partners too,
 * and move to the same row when that keeps everyone below their parents. The order within a row
 * follows the parents' order, and positions alternate between centring children under their
 * parents and parents over their children.
 */
export function familyLayout(
  nodes: LayoutNode[],
  links: LayoutLink[],
  options: LayoutOptions,
  marriages: LayoutMarriage[] = [],
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

  const spouses = marriages.filter(
    (marriage) =>
      index.has(marriage.spouseId) &&
      index.has(marriage.otherSpouseId) &&
      marriage.spouseId !== marriage.otherSpouseId,
  );
  const rank = ranks(nodes, parents, children, spouses);
  const rows = new Map<number, string[]>();
  for (const node of nodes) {
    rows.set(rank.get(node.id)!, [...(rows.get(rank.get(node.id)!) ?? []), node.id]);
  }
  const rowRanks = [...rows.keys()].sort((a, b) => a - b);

  // Partners are married or share a child; only partners on the same row are kept next to each other.
  const partners = new Map<string, Set<string>>(nodes.map((node) => [node.id, new Set()]));
  const pairs = [
    ...[...parents.values()].flatMap((ids) => ids.flatMap((a) => ids.map((b) => [a, b]))),
    ...spouses.map((marriage) => [marriage.spouseId, marriage.otherSpouseId]),
  ];
  for (const [a, b] of pairs) {
    if (a !== b && rank.get(a) === rank.get(b)) {
      partners.get(a)!.add(b);
      partners.get(b)!.add(a);
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
        // Whoever's parents are further left goes first, with each pair of partners side by side.
        const side = (id: string) => ownParents(id) ?? key;
        orderCouple(ids, side, partners, index);
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

  const start = new Map(
    nodes.map((node) => [
      node.id,
      { x: x.get(node.id)!, y: rank.get(node.id)! * (options.height + options.rankSep) },
    ]),
  );
  if (options.settle === false) {
    return start;
  }
  const byRank = [...nodes].sort((a, b) => rank.get(a.id)! - rank.get(b.id)!).map((n) => n.id);
  return settle(start, byRank, parents, couple, width, options);

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

/** Couples of more people than this keep the order of their parents' sides. */
const maxCoupleToArrange = 7;

/**
 * Orders the people of one couple in place. Someone with two partners stands between them, so
 * nobody stands between two partners where that can be avoided; then whoever's parents are further
 * left goes first.
 */
function orderCouple(
  ids: string[],
  side: (id: string) => number,
  partners: Map<string, Set<string>>,
  index: Map<string, number>,
): void {
  ids.sort((a, b) => side(a) - side(b) || index.get(a)! - index.get(b)!);
  if (ids.length < 3 || ids.length > maxCoupleToArrange) {
    return;
  }
  const bySide = new Map(ids.map((id, i) => [id, i]));
  const pairs = ids.flatMap((a) =>
    [...partners.get(a)!].filter((b) => bySide.has(b) && a < b).map((b) => [a, b] as const),
  );
  // Spread between partners first, then distance from the parents' order.
  const cost = (order: string[]): [number, number] => {
    const at = new Map(order.map((id, i) => [id, i]));
    return [
      pairs.reduce((sum, [a, b]) => sum + Math.abs(at.get(a)! - at.get(b)!) - 1, 0),
      order.reduce((sum, id, i) => sum + Math.abs(i - bySide.get(id)!), 0),
    ];
  };
  let best = [...ids];
  let bestCost = cost(best);
  const permute = (order: string[], rest: string[]): void => {
    if (!rest.length) {
      const c = cost(order);
      if (c[0] < bestCost[0] || (c[0] === bestCost[0] && c[1] < bestCost[1])) {
        best = [...order];
        bestCost = c;
      }
      return;
    }
    for (let i = 0; i < rest.length; i++) {
      permute([...order, rest[i]], [...rest.slice(0, i), ...rest.slice(i + 1)]);
    }
  };
  permute([], ids);
  ids.splice(0, ids.length, ...best);
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

/**
 * Each person's row: below all their parents, and as close above their children as that allows.
 * Then the higher of two spouses moves down to the other's row, taking their descendants down
 * with them where needed, so every child stays below its parents.
 */
function ranks(
  nodes: LayoutNode[],
  parents: Map<string, string[]>,
  children: Map<string, string[]>,
  spouses: LayoutMarriage[],
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
  // Moving someone down never puts them above a parent; their children follow if they must.
  const moveDown = (id: string, to: number, depth: number): void => {
    if (rank.get(id)! >= to || depth > nodes.length) {
      return;
    }
    rank.set(id, to);
    for (const child of children.get(id)!) {
      moveDown(child, to + 1, depth + 1);
    }
  };
  // A few rounds let a chain of marriages settle.
  for (let round = 0; round < 3; round++) {
    for (const { spouseId, otherSpouseId } of spouses) {
      moveDown(spouseId, rank.get(otherSpouseId)!, 0);
      moveDown(otherSpouseId, rank.get(spouseId)!, 0);
    }
  }
  return rank;
}

/** Steps of the settling; each moves people less than the one before. */
const settleSteps = 300;
const gravity = 0.02;

/**
 * Lets people move off their rows into the positions that suit the lines best: every link pulls
 * its two people towards one row apart, and people push apart from those near them. Each couple
 * moves as one, so partners stay on one row next to each other with nobody between them. Children
 * always stay at least half a row below each of their parents, and people never overlap. Nothing
 * is random, so the same family always looks the same.
 */
function settle(
  start: Map<string, Point>,
  byRank: string[],
  parents: Map<string, string[]>,
  couple: Map<string, string>,
  width: Map<string, number>,
  options: LayoutOptions,
): Map<string, Point> {
  const ids = [...start.keys()];
  const n = ids.length;
  const at = new Map(ids.map((id, i) => [id, i]));
  const widths = Float64Array.from(ids, (id) => width.get(id)!);
  const row = options.height + options.rankSep;
  const below = options.height + options.rankSep / 2;
  // People further apart than this do not push each other.
  const reach = row * 2;

  // Couples in the order of their rows, each person at a fixed offset from their couple's middle.
  const unitKeys = [...new Set(byRank.map((id) => couple.get(id) ?? id))];
  const unitAt = new Map(unitKeys.map((key, u) => [key, u]));
  const units = unitKeys.length;
  const unit = Int32Array.from(ids, (id) => unitAt.get(couple.get(id) ?? id)!);
  const size = new Float64Array(units);
  const ux = new Float64Array(units);
  const uy = new Float64Array(units);
  for (let j = 0; j < n; j++) {
    size[unit[j]]++;
    ux[unit[j]] += start.get(ids[j])!.x;
    uy[unit[j]] = start.get(ids[j])!.y;
  }
  for (let u = 0; u < units; u++) {
    ux[u] /= size[u];
  }
  const offset = Float64Array.from(ids, (id, j) => start.get(id)!.x - ux[unit[j]]);
  const xs = new Float64Array(n);
  const ys = new Float64Array(n);
  const place = () => {
    for (let j = 0; j < n; j++) {
      xs[j] = ux[unit[j]] + offset[j];
      ys[j] = uy[unit[j]];
    }
  };

  const springs: [number, number][] = [];
  for (const [child, own] of parents) {
    for (const parent of own) {
      springs.push([at.get(parent)!, at.get(child)!]);
    }
  }
  // Each couple's parents' couples, in the order of the rows.
  const above = unitKeys.map(() => new Set<number>());
  for (const id of byRank) {
    for (const parent of parents.get(id)!) {
      above[unit[at.get(id)!]].add(unit[at.get(parent)!]);
    }
  }

  const mx = new Float64Array(units);
  const my = new Float64Array(units);
  let step = row / 2;
  for (let i = 0; i < settleSteps; i++) {
    place();
    mx.fill(0);
    my.fill(0);
    forNearby(reach, (j, k, dx, dy) => {
      const squared = Math.max(dx * dx + dy * dy, 0.01);
      const force = (0.1 * row * row) / squared;
      mx[unit[j]] += dx * force;
      my[unit[j]] += dy * force;
      mx[unit[k]] -= dx * force;
      my[unit[k]] -= dy * force;
    });
    for (const [a, b] of springs) {
      const dx = xs[b] - xs[a];
      const dy = ys[b] - ys[a];
      const distance = Math.hypot(dx, dy) || 0.01;
      const force = (distance - row) / distance / 2;
      mx[unit[a]] += dx * force;
      my[unit[a]] += dy * force;
      mx[unit[b]] -= dx * force;
      my[unit[b]] -= dy * force;
    }
    // A light pull to the middle keeps separate families from drifting apart.
    let middleX = 0;
    let middleY = 0;
    for (let j = 0; j < n; j++) {
      middleX += xs[j] / n;
      middleY += ys[j] / n;
    }
    for (let u = 0; u < units; u++) {
      const x = mx[u] / size[u] + (middleX - ux[u]) * gravity;
      const y = my[u] / size[u] + (middleY - uy[u]) * gravity;
      const length = Math.hypot(x, y);
      const scale = length > step ? step / length : 1;
      ux[u] += x * scale;
      uy[u] += y * scale;
    }
    for (let u = 0; u < units; u++) {
      for (const parent of above[u]) {
        uy[u] = Math.max(uy[u], uy[parent] + below);
      }
    }
    step = Math.max(step * 0.98, 0.5);
  }

  // Couples closer than their people's size allows move apart sideways, so rows below stay below.
  for (let round = 0; round < 50; round++) {
    place();
    let moved = false;
    forNearby(row, (j, k, dx, dy) => {
      const overlap = (widths[j] + widths[k]) / 2 + options.nodeSep / 2 - Math.abs(dx);
      if (overlap > 0 && Math.abs(dy) < options.height + options.nodeSep / 2) {
        const sign = dx > 0 || (dx === 0 && j > k) ? 1 : -1;
        ux[unit[j]] += (sign * overlap) / 2;
        ux[unit[k]] -= (sign * overlap) / 2;
        moved = true;
      }
    });
    if (!moved) {
      break;
    }
  }
  place();
  return new Map(ids.map((id, j) => [id, { x: xs[j], y: ys[j] }]));

  /** Calls `visit` once for each pair of people of different couples within `distance`. */
  function forNearby(
    distance: number,
    visit: (j: number, k: number, dx: number, dy: number) => void,
  ): void {
    const cells = new Map<string, number[]>();
    for (let j = 0; j < n; j++) {
      const key = `${Math.floor(xs[j] / distance)},${Math.floor(ys[j] / distance)}`;
      const cell = cells.get(key);
      if (cell) {
        cell.push(j);
      } else {
        cells.set(key, [j]);
      }
    }
    for (let j = 0; j < n; j++) {
      const cx = Math.floor(xs[j] / distance);
      const cy = Math.floor(ys[j] / distance);
      for (let ox = -1; ox <= 1; ox++) {
        for (let oy = -1; oy <= 1; oy++) {
          for (const k of cells.get(`${cx + ox},${cy + oy}`) ?? []) {
            if (k > j && unit[j] !== unit[k]) {
              const dx = xs[j] - xs[k];
              const dy = ys[j] - ys[k];
              if (Math.abs(dx) <= distance && Math.abs(dy) <= distance) {
                visit(j, k, dx, dy);
              }
            }
          }
        }
      }
    }
  }
}
