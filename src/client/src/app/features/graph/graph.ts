import { PersonLabel } from '../people/person';
import { MarriageType, ParentChildType, relationWord } from '../relationships/relationship';

/**
 * A person as you may see them in the graph. `maintainedByYou` never names the maintaining
 * account. A person hidden from you is a `Private` placeholder that keeps its links (PRD 05).
 */
export interface GraphPerson extends PersonLabel {
  id: string;
  maintainedByYou: boolean;
  /** Null when not set and for a placeholder (D-037). */
  description: string | null;
}

/**
 * A person in a local view. `generation` is above (positive) or below (negative) the centre;
 * `hasMore` means they have relatives not in that view (D-010).
 */
export interface LocalViewPerson {
  person: GraphPerson;
  generation: number;
  hasMore: boolean;
}

/** An active parent–child link. */
export interface GraphLink {
  id: string;
  parentId: string;
  childId: string;
  type: ParentChildType;
}

/** An active marriage between two people of the same generation; `isFormer` when it ended (D-031). */
export interface GraphMarriage {
  id: string;
  firstSpouseId: string;
  secondSpouseId: string;
  isFormer: boolean;
}

/** The centre and the people nearest to it, from `GET /api/people/{id}/graph` (D-023). */
export interface LocalView {
  centreId: string;
  people: LocalViewPerson[];
  links: GraphLink[];
  marriages: GraphMarriage[];
  isTruncated: boolean;
}

/**
 * Everyone connected to your profiles, nearest first, from `GET /api/graph/overview` (D-024).
 * Nobody is the centre; `isTruncated` says more people were in reach.
 */
export interface GraphOverview {
  people: GraphPerson[];
  links: GraphLink[];
  marriages: GraphMarriage[];
  isTruncated: boolean;
}

/**
 * How `person` relates to the previous person on the path, through `linkId` (PRD 08): a spouse
 * step has the type `Marriage` or `FormerMarriage` (D-031).
 */
export interface ConnectionStep {
  linkId: string;
  relation: 'Parent' | 'Child' | 'Spouse';
  type: ParentChildType | MarriageType;
  person: GraphPerson;
}

export interface Connection {
  start: GraphPerson;
  steps: ConnectionStep[];
}

/**
 * The shortest path first, then further paths, from `GET /api/people/{id}/connections/{otherId}`.
 * With `searchComplete` false, no path means none was found within the search limit (D-009).
 */
export interface Connections {
  paths: Connection[];
  morePathsExist: boolean;
  searchComplete: boolean;
}

/**
 * A step's wording, from the gender of the step's person where it is known: "mother", "son",
 * "wife", or "parent", "child" and "spouse" otherwise (D-030, D-031). An adoptive step reads
 * "adoptive …" or "adopted …" (AC-08.7), an ended marriage "former …".
 */
export function stepLabel(step: Pick<ConnectionStep, 'relation' | 'type' | 'person'>): string {
  const relation = ({ Parent: 'parent', Child: 'child', Spouse: 'spouse' } as const)[step.relation];
  return relationWord(relation, step.person.gender, step.type);
}
