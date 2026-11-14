import { GraphLink, GraphMarriage } from './graph';

/** One line to a child from the middle of their parents' marriage line (D-054). */
export interface CoupleLine {
  /** The marriage whose line it starts from. */
  marriageId: string;
  childId: string;
  /** Both parents' links to the child, of one type. */
  links: [GraphLink, GraphLink];
}

/** The lines to draw for the shown links: one per child of a married couple, the rest as they are. */
export interface ChildLines {
  couples: CoupleLine[];
  /** Links to a child with no other shown parent, whose line the parent's other such children share (D-055). */
  lone: GraphLink[];
  /** Links that keep their own line from the parent. */
  single: GraphLink[];
}

/**
 * Joins a child's two links into one line when both parents are shown, are or were married to each
 * other and are the same kind of parent to the child (D-054, former marriages D-055). A parent not
 * shown, or one biological and one adoptive parent leave each link its own line, so the dashes of an adoptive
 * link never cover a biological one. A child with only one shown parent is `lone` (D-055). Links
 * keep the order they came in.
 */
export function childLines(
  shownIds: ReadonlySet<string>,
  links: readonly GraphLink[],
  marriages: readonly GraphMarriage[],
): ChildLines {
  const pair = (a: string, b: string) => (a < b ? `${a} ${b}` : `${b} ${a}`);
  const married = new Map<string, string>();
  // A couple married again after a former marriage use the line of the current one.
  for (const marriage of [...marriages].sort((a, b) => Number(a.isFormer) - Number(b.isFormer))) {
    const key = pair(marriage.firstSpouseId, marriage.secondSpouseId);
    if (
      !married.has(key) &&
      shownIds.has(marriage.firstSpouseId) &&
      shownIds.has(marriage.secondSpouseId)
    ) {
      married.set(key, marriage.id);
    }
  }

  const shown = links.filter((link) => shownIds.has(link.parentId) && shownIds.has(link.childId));
  const byChild = new Map<string, GraphLink[]>();
  for (const link of shown) {
    byChild.set(link.childId, [...(byChild.get(link.childId) ?? []), link]);
  }

  const joined = new Set<string>();
  const couples: CoupleLine[] = [];
  for (const link of shown) {
    if (joined.has(link.id)) {
      continue;
    }
    const other = byChild
      .get(link.childId)!
      .find(
        (candidate) =>
          candidate.id !== link.id &&
          !joined.has(candidate.id) &&
          candidate.parentId !== link.parentId &&
          candidate.type === link.type &&
          married.has(pair(link.parentId, candidate.parentId)),
      );
    if (other) {
      joined.add(link.id).add(other.id);
      couples.push({
        marriageId: married.get(pair(link.parentId, other.parentId))!,
        childId: link.childId,
        links: [link, other],
      });
    }
  }
  const rest = shown.filter((link) => !joined.has(link.id));
  const lone = (link: GraphLink) => byChild.get(link.childId)!.length === 1;
  return { couples, lone: rest.filter(lone), single: rest.filter((link) => !lone(link)) };
}

/** The bar above a couple's children, from its leftmost to its rightmost end. */
export interface TrunkBar {
  id: string;
  from: number;
  to: number;
}

/**
 * A height for each bar between two rows, 0 nearest the parents, so that bars which would run
 * over each other sit at different heights and each child's line stays its own (D-054). Bars take
 * the first height that is free, left to right; `margin` is the least space between two bars at
 * one height.
 */
export function trunkLevels(bars: readonly TrunkBar[], margin: number): Map<string, number> {
  const ends: number[] = [];
  const levels = new Map<string, number>();
  for (const bar of [...bars].sort((a, b) => a.from - b.from || a.to - b.to)) {
    let level = ends.findIndex((end) => end + margin <= bar.from);
    if (level < 0) {
      level = ends.length;
    }
    ends[level] = bar.to;
    levels.set(bar.id, level);
  }
  return levels;
}
