import { Gender, PersonLabel } from '../people/person';

/** The only parent–child types (PRD 02). */
export type ParentChildType = 'Biological' | 'Adoptive';

/** A marriage, current or ended (D-031). */
export type MarriageType = 'Marriage' | 'FormerMarriage';

/**
 * How a parent, child or spouse is named: "mother", "adopted son", "former wife", or "parent",
 * "child" and "spouse" when the gender is Other, not set or hidden (D-030, D-031). Adoption and a
 * former marriage are always named (AC-08.7).
 */
export function relationWord(
  relation: 'parent' | 'child' | 'spouse',
  gender: Gender | null,
  type: ParentChildType | MarriageType,
): string {
  const words = {
    parent: { Male: 'father', Female: 'mother', Other: 'parent' },
    child: { Male: 'son', Female: 'daughter', Other: 'child' },
    spouse: { Male: 'husband', Female: 'wife', Other: 'spouse' },
  }[relation];
  const word = words[gender ?? 'Other'];
  switch (type) {
    case 'Adoptive':
      return relation === 'parent' ? `adoptive ${word}` : `adopted ${word}`;
    case 'FormerMarriage':
      return `former ${word}`;
    default:
      return word;
  }
}

/** The wording in brackets after a relative's name; nothing for a plain "parent", "child" or "spouse". */
export function relationHint(
  relation: 'parent' | 'child' | 'spouse',
  gender: Gender | null,
  type: ParentChildType | MarriageType,
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

/** A new marriage; the order of the two people does not matter. */
export interface NewMarriage {
  spouseId: string;
  otherSpouseId: string;
}

/** The spouses are in ID order. `isActive` is false while the other maintainer has not approved. */
export interface CreatedMarriage {
  id: string;
  firstSpouseId: string;
  secondSpouseId: string;
  isActive: boolean;
}

/** A spouse through an active marriage; `isFormer` when the marriage ended (D-031). */
export interface Spouse extends PersonLabel {
  marriageId: string;
  id: string;
  maintainedByYou: boolean;
  isFormer: boolean;
}

/** A person's spouses, current ones first, from `GET /api/people/{id}/spouses`. */
export interface SpouseList {
  spouses: Spouse[];
  isTruncated: boolean;
}

/** A marriage's consent on both sides, shown to the maintainers of its spouses (D-031). */
export interface MarriageConsent {
  id: string;
  first: LinkEndpointConsent;
  second: LinkEndpointConsent;
  isFormer: boolean;
  isActive: boolean;
}

/** The inactive marriages of your profiles, from `GET /api/marriages/inactive`. */
export interface MarriageConsentList {
  marriages: MarriageConsent[];
  isTruncated: boolean;
}
