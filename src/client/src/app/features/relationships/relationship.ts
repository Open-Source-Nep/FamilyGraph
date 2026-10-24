import { Gender, PersonLabel } from '../people/person';

/** The only directly registered relationship types (PRD 02). */
export type ParentChildType = 'Biological' | 'Adoptive';

/**
 * How a parent or child is named: "mother", "adopted son", or "parent" and "child" when the
 * gender is Other, not set or hidden (D-030). Adoption is always named (AC-08.7).
 */
export function relationWord(
  relation: 'parent' | 'child',
  gender: Gender | null,
  type: ParentChildType,
): string {
  const words =
    relation === 'parent'
      ? { Male: 'father', Female: 'mother', Other: 'parent' }
      : { Male: 'son', Female: 'daughter', Other: 'child' };
  const word = words[gender ?? 'Other'];
  if (type !== 'Adoptive') {
    return word;
  }
  return relation === 'parent' ? `adoptive ${word}` : `adopted ${word}`;
}

/** The wording in brackets after a relative's name; nothing for a plain "parent" or "child". */
export function relationHint(
  relation: 'parent' | 'child',
  gender: Gender | null,
  type: ParentChildType,
): string | null {
  const word = relationWord(relation, gender, type);
  return word === relation ? null : word;
}

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

/**
 * A relative through an active link. `maintainedByYou` never names the maintaining account. A
 * relative hidden from you is a placeholder, with the link's type still shown (PRD 05).
 */
export interface Relative extends PersonLabel {
  linkId: string;
  id: string;
  type: ParentChildType;
  maintainedByYou: boolean;
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

/** Which side of a link a consent request is for. */
export type LinkEndpoint = 'Parent' | 'Child';

/** One endpoint's consent (PRD 03, D-013). Nothing returns to pending. */
export type EndpointApproval = 'Pending' | 'Approved' | 'Disagreed';

export interface LinkEndpointConsent extends PersonLabel {
  id: string;
  approval: EndpointApproval;
  maintainedByYou: boolean;
}

/** A link's per-endpoint consent, shown to the maintainers of its profiles (D-021). */
export interface LinkConsent {
  id: string;
  type: ParentChildType;
  parent: LinkEndpointConsent;
  child: LinkEndpointConsent;
  isActive: boolean;
}

/** The inactive links of your profiles, from `GET /api/relationships/inactive`. */
export interface LinkConsentList {
  links: LinkConsent[];
  isTruncated: boolean;
}
