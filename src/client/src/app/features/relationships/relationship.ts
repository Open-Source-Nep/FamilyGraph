import { Gender, PersonLabel } from '../people/person';

/** The only parent–child types (PRD 02). */
export type ParentChildType = 'Biological' | 'Adoptive';

/** A marriage, current or ended (D-031). */
export type MarriageType = 'Marriage' | 'FormerMarriage';

type Relation = 'parent' | 'child' | 'spouse';

/**
 * Every wording as a whole phrase, so that a translation may order or inflect the words as its
 * language needs (D-047). `Other` also stands for a gender not set or hidden.
 */
const relationWords: Record<Relation, Record<'plain' | 'marked', Record<Gender, string>>> = {
  parent: {
    plain: {
      Male: $localize`:@@relation.father:father`,
      Female: $localize`:@@relation.mother:mother`,
      Other: $localize`:@@relation.parent:parent`,
    },
    marked: {
      Male: $localize`:@@relation.adoptiveFather:adoptive father`,
      Female: $localize`:@@relation.adoptiveMother:adoptive mother`,
      Other: $localize`:@@relation.adoptiveParent:adoptive parent`,
    },
  },
  child: {
    plain: {
      Male: $localize`:@@relation.son:son`,
      Female: $localize`:@@relation.daughter:daughter`,
      Other: $localize`:@@relation.child:child`,
    },
    marked: {
      Male: $localize`:@@relation.adoptedSon:adopted son`,
      Female: $localize`:@@relation.adoptedDaughter:adopted daughter`,
      Other: $localize`:@@relation.adoptedChild:adopted child`,
    },
  },
  spouse: {
    plain: {
      Male: $localize`:@@relation.husband:husband`,
      Female: $localize`:@@relation.wife:wife`,
      Other: $localize`:@@relation.spouse:spouse`,
    },
    marked: {
      Male: $localize`:@@relation.formerHusband:former husband`,
      Female: $localize`:@@relation.formerWife:former wife`,
      Other: $localize`:@@relation.formerSpouse:former spouse`,
    },
  },
};

/** Adoption marks a parent or child, a former marriage a spouse (AC-08.7). */
function isMarked(relation: Relation, type: ParentChildType | MarriageType): boolean {
  return relation === 'spouse' ? type === 'FormerMarriage' : type === 'Adoptive';
}

/**
 * How a parent, child or spouse is named: "mother", "adopted son", "former wife", or "parent",
 * "child" and "spouse" when the gender is Other, not set or hidden (D-030, D-031). Adoption and a
 * former marriage are always named (AC-08.7).
 */
export function relationWord(
  relation: Relation,
  gender: Gender | null,
  type: ParentChildType | MarriageType,
): string {
  return relationWords[relation][isMarked(relation, type) ? 'marked' : 'plain'][gender ?? 'Other'];
}

/** The wording in brackets after a relative's name; nothing for a plain "parent", "child" or "spouse". */
export function relationHint(
  relation: Relation,
  gender: Gender | null,
  type: ParentChildType | MarriageType,
): string | null {
  return (gender ?? 'Other') === 'Other' && !isMarked(relation, type)
    ? null
    : relationWord(relation, gender, type);
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
