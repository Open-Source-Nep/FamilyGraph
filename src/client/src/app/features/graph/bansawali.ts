import { fullName } from '../people/person';
import { relationWord } from '../relationships/relationship';
import { compareChildren } from './child-order';
import { GraphLink, GraphMarriage, GraphPerson } from './graph';

/** Whose children the Bansawali follows: sons only (the default) or every child (D-043). */
export type BansawaliLine = 'Sons' | 'AllChildren';

/**
 * A person in a Bansawali: `generation` below the ancestor (0 for the ancestor), whether the line
 * `continues` through them, and `hasMore` when their children were cut off by the limit.
 */
export interface BansawaliPerson {
  person: GraphPerson;
  generation: number;
  continues: boolean;
  hasMore: boolean;
}

/** A Bansawali from `GET /api/people/{id}/bansawali?line=` (PRD 08, D-043). */
export interface Bansawali {
  rootId: string;
  line: BansawaliLine;
  people: BansawaliPerson[];
  /** The link that places each descendant under one parent. */
  links: GraphLink[];
  /** People married to someone in the Bansawali, who never continue the line. */
  spouses: GraphPerson[];
  marriages: GraphMarriage[];
  isTruncated: boolean;
}

/** A spouse beside a person: "wife Sita", or "former wife Sita". */
export interface BansawaliSpouse {
  person: GraphPerson;
  label: string;
}

/** A person in the outline with their spouses and, in birth order, their children in the Bansawali. */
export interface BansawaliEntry {
  row: BansawaliPerson;
  /** "son", "daughter", "adopted son", "child"; null for the ancestor. */
  relation: string | null;
  adopted: boolean;
  spouses: BansawaliSpouse[];
  children: BansawaliEntry[];
}

/**
 * The Bansawali as an outline from the ancestor (D-043): each person's children in birth order
 * (D-039), with their spouses beside them, current ones first. Returns null without the ancestor.
 */
export function bansawaliTree(bansawali: Bansawali): BansawaliEntry | null {
  const rows = new Map(bansawali.people.map((row) => [row.person.id, row]));
  const spouses = new Map(
    [...bansawali.people.map((row) => row.person), ...bansawali.spouses].map((person) => [
      person.id,
      person,
    ]),
  );
  const childLinks = new Map<string, GraphLink[]>();
  for (const link of bansawali.links) {
    if (rows.has(link.parentId) && rows.has(link.childId)) {
      childLinks.set(link.parentId, [...(childLinks.get(link.parentId) ?? []), link]);
    }
  }

  const spousesOf = (personId: string): BansawaliSpouse[] =>
    bansawali.marriages
      .filter((m) => m.firstSpouseId === personId || m.secondSpouseId === personId)
      .map((m) => ({
        marriage: m,
        person: spouses.get(m.firstSpouseId === personId ? m.secondSpouseId : m.firstSpouseId),
      }))
      .filter((entry): entry is { marriage: GraphMarriage; person: GraphPerson } => !!entry.person)
      .sort(
        (a, b) =>
          Number(a.marriage.isFormer) - Number(b.marriage.isFormer) ||
          fullName(a.person).localeCompare(fullName(b.person)),
      )
      .map(({ marriage, person }) => ({
        person,
        label: relationWord(
          'spouse',
          person.gender,
          marriage.isFormer ? 'FormerMarriage' : 'Marriage',
        ),
      }));

  const entry = (row: BansawaliPerson, link: GraphLink | null): BansawaliEntry => ({
    row,
    relation: link ? relationWord('child', row.person.gender, link.type) : null,
    adopted: link?.type === 'Adoptive',
    spouses: spousesOf(row.person.id),
    children: (childLinks.get(row.person.id) ?? [])
      .map((child) => ({ person: rows.get(child.childId)!.person, link: child }))
      .sort(compareChildren)
      .map(({ person, link: child }) => entry(rows.get(person.id)!, child)),
  });

  const root = rows.get(bansawali.rootId);
  return root ? entry(root, null) : null;
}

/** "Neupane Bansawali" from the ancestor's last name, or "Bansawali of Hari" without one. */
export function bansawaliTitle(ancestor: GraphPerson): string {
  return ancestor.lastName && !ancestor.placeholder
    ? `${ancestor.lastName} Bansawali`
    : `Bansawali of ${fullName(ancestor)}`;
}

/** How many generations one section of the outline shows before a line continues in another (D-045). */
export const sectionGenerations = 8;

/** A numbered part of the outline, starting from the ancestor or from someone deeper in the line. */
export interface BansawaliSection {
  number: number;
  entry: BansawaliEntry;
  /** The section where this person's children say "continued in", null for the first. */
  from: number | null;
}

/** The sections in order and, by person ID, the section where each cut-off person's children continue. */
export interface BansawaliSections {
  sections: BansawaliSection[];
  continuedIn: Map<string, number>;
}

/**
 * Splits the outline so that no section is deeper than `generations` (D-045): someone in a
 * section's last generation who has children in the Bansawali starts a new section, numbered in
 * the order the sections are reached, and their row points to it.
 */
export function bansawaliSections(
  root: BansawaliEntry,
  generations = sectionGenerations,
): BansawaliSections {
  const sections: BansawaliSection[] = [];
  const continuedIn = new Map<string, number>();
  const queue: { entry: BansawaliEntry; from: number | null }[] = [{ entry: root, from: null }];
  while (queue.length) {
    const { entry: start, from } = queue.shift()!;
    const number = sections.length + 1;
    sections.push({ number, entry: start, from });
    const walk = (entry: BansawaliEntry): void => {
      for (const child of entry.children) {
        if (
          child.children.length &&
          child.row.generation - start.row.generation === generations - 1
        ) {
          queue.push({ entry: child, from: number });
          continuedIn.set(child.row.person.id, sections.length + queue.length);
        } else {
          walk(child);
        }
      }
    };
    walk(start);
  }
  return { sections, continuedIn };
}
