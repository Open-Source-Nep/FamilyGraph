import { PersonName } from '../people/person';

/** The only directly registered relationship types (PRD 02). */
export type ParentChildType = 'Biological' | 'Adoptive';

export interface NewLink {
  parentId: string;
  childId: string;
  type: ParentChildType;
}

/** `isActive` is false while the other profile's maintainer has not approved the link (PRD 03). */
export interface CreatedLink extends NewLink {
  id: string;
  isActive: boolean;
}

export interface Relative extends PersonName {
  id: string;
  type: ParentChildType;
}

/** `birthOrder` is null while the parent's maintainer has not placed the child (D-016). */
export interface Child extends Relative {
  birthOrder: number | null;
}

/** Parents and children through active links, from `GET /api/people/{id}/relatives`. */
export interface DirectRelatives {
  parents: Relative[];
  parentsTruncated: boolean;
  children: Child[];
  childrenTruncated: boolean;
}
