import { PersonLabel } from '../people/person';
import { ParentChildType } from '../relationships/relationship';

/**
 * A person as you may see them in the graph. `maintainedByYou` never names the maintaining
 * account. A person hidden from you is a `Private` placeholder that keeps its links (PRD 05).
 */
export interface GraphPerson extends PersonLabel {
  id: string;
  maintainedByYou: boolean;
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

/** The centre and the people nearest to it, from `GET /api/people/{id}/graph` (D-023). */
export interface LocalView {
  centreId: string;
  people: LocalViewPerson[];
  links: GraphLink[];
  isTruncated: boolean;
}

/**
 * Everyone connected to your profiles, nearest first, from `GET /api/graph/overview` (D-024).
 * Nobody is the centre; `isTruncated` says more people were in reach.
 */
export interface GraphOverview {
  people: GraphPerson[];
  links: GraphLink[];
  isTruncated: boolean;
}

/** How `person` relates to the previous person on the path, through `linkId` (PRD 08). */
export interface ConnectionStep {
  linkId: string;
  relation: 'Parent' | 'Child';
  type: ParentChildType;
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

/** A step's wording: an adoptive step reads "adoptive parent" or "adopted child" (AC-08.7). */
export function stepLabel(step: Pick<ConnectionStep, 'relation' | 'type'>): string {
  const adoptive = step.type === 'Adoptive';
  if (step.relation === 'Parent') {
    return adoptive ? 'adoptive parent' : 'parent';
  }
  return adoptive ? 'adopted child' : 'child';
}
