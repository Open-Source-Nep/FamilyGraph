import { GraphLink, GraphOverview, GraphPerson, LocalView, LocalViewPerson } from './graph';

/**
 * The people and links on screen: a local view plus the views loaded around edge people (AC-08.5),
 * or the home page's overview, which has no centre (`centreId` null, every generation 0).
 * `generation` is relative to the first view's centre. People keep the order they were found in,
 * nearest first.
 */
export interface ShownGraph {
  centreId: string | null;
  people: LocalViewPerson[];
  links: GraphLink[];
}

/** The most people the client keeps on screen; past it, centre the view on someone instead. */
export const maxShownPeople = 500;

export function fromView(view: LocalView): ShownGraph {
  return { centreId: view.centreId, people: view.people, links: view.links };
}

/** The overview as people on screen: nobody is the centre and nobody has more to load. */
export function fromOverview(overview: GraphOverview): ShownGraph {
  return {
    centreId: null,
    people: overview.people.map((person) => ({ person, generation: 0, hasMore: false })),
    links: overview.links,
  };
}

/**
 * Adds a view centred on a shown person. A person's `hasMore` stays true only while every view
 * that included them says so: `false` means all their relatives were in a view already merged.
 * The anchor loses it when the view added nobody, so "load more" never repeats a no-op.
 */
export function expand(shown: ShownGraph, view: LocalView): { graph: ShownGraph; added: number } {
  const anchor = shown.people.find((row) => row.person.id === view.centreId);
  const offset = anchor?.generation ?? 0;
  const incoming = new Map(view.people.map((row) => [row.person.id, row]));
  const known = new Set(shown.people.map((row) => row.person.id));

  const added = view.people
    .filter((row) => !known.has(row.person.id))
    .map((row) => ({ ...row, generation: row.generation + offset }));
  const people = shown.people.map((row) => {
    const other = incoming.get(row.person.id);
    const hasMore =
      row.hasMore &&
      (other?.hasMore ?? true) &&
      !(row.person.id === view.centreId && !added.length);
    return hasMore === row.hasMore ? row : { ...row, hasMore };
  });

  const linkIds = new Set(shown.links.map((link) => link.id));
  const links = [...shown.links, ...view.links.filter((link) => !linkIds.has(link.id))];
  return { graph: { ...shown, people: [...people, ...added], links }, added: added.length };
}

/** A shown person with their shown parents and children, for the text view (AC-09.2). */
export interface ShownRelatives {
  row: LocalViewPerson;
  parents: { person: GraphPerson; link: GraphLink }[];
  children: { person: GraphPerson; link: GraphLink }[];
}

/** Shown people grouped by generation, oldest first, each with their shown parents and children. */
export function byGeneration(
  shown: ShownGraph,
): { generation: number; people: ShownRelatives[] }[] {
  const people = new Map(shown.people.map((row) => [row.person.id, row.person]));
  const groups = new Map<number, ShownRelatives[]>();
  for (const row of shown.people) {
    const id = row.person.id;
    const entry: ShownRelatives = {
      row,
      parents: shown.links
        .filter((link) => link.childId === id && people.has(link.parentId))
        .map((link) => ({ person: people.get(link.parentId)!, link })),
      children: shown.links
        .filter((link) => link.parentId === id && people.has(link.childId))
        .map((link) => ({ person: people.get(link.childId)!, link })),
    };
    groups.set(row.generation, [...(groups.get(row.generation) ?? []), entry]);
  }
  return [...groups.entries()]
    .sort(([a], [b]) => b - a)
    .map(([generation, people]) => ({ generation, people }));
}

/** "2 generations above", "Same generation", "1 generation below". */
export function generationLabel(generation: number): string {
  if (generation === 0) {
    return 'Same generation';
  }
  const count = Math.abs(generation);
  return `${count} generation${count === 1 ? '' : 's'} ${generation > 0 ? 'above' : 'below'}`;
}
