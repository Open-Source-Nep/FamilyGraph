/** A person's name. Only the first name is required (PRD 01). */
export interface PersonName {
  firstName: string;
  middleName: string | null;
  lastName: string | null;
}

/** What a profile form saves: the name and the optional gender (D-030). */
export interface PersonDetails {
  name: PersonName;
  gender: Gender | null;
}

/** Public profiles show their name to signed-in users; Private ones only to their maintainer and administrators (PRD 05). */
export type Visibility = 'Public' | 'Private';

/** A person's gender, if set; optional like everything but the first name (D-030). */
export type Gender = 'Male' | 'Female' | 'Other';

/** The genders in the order the forms offer them. */
export const genders: readonly Gender[] = ['Male', 'Female', 'Other'];

/** What you see instead of a Private profile hidden from you or a deleted one. It carries no name or details (PRD 05). */
export type Placeholder = 'Private' | 'Deleted';

/** A person as the server lets you see them: a profile hidden from you has a `placeholder` and no name. */
export interface PersonLabel {
  firstName: string | null;
  middleName: string | null;
  lastName: string | null;
  placeholder: Placeholder | null;
  visibility: Visibility;
  /** The current image, or null without one. A placeholder never has one (D-028). */
  imageId: string | null;
  /** Null when not set. A placeholder never has one (D-030). */
  gender: Gender | null;
}

/** A profile as the signed-in user sees it, from `GET /api/people/{id}`. */
export interface Person extends PersonLabel {
  id: string;
  maintainedByYou: boolean;
}

/** One of your profiles, which you always see by name. */
export interface PersonSummary extends PersonName {
  id: string;
  visibility: Visibility;
  gender: Gender | null;
}

/** `isTruncated` says the list stopped at the server's limit. */
export interface ProfileList {
  people: PersonSummary[];
  isTruncated: boolean;
}

/** People found by name (D-032); `isTruncated` says more match, so refine the search. */
export interface PersonSearchResult {
  people: Person[];
  isTruncated: boolean;
}

/** The person's image; the image ID in the URL changes with each new image. */
export function imageUrl(person: { id: string; imageId: string | null }): string | null {
  return person.imageId
    ? `/api/people/${encodeURIComponent(person.id)}/image?v=${encodeURIComponent(person.imageId)}`
    : null;
}

/** The longest name part the server accepts. */
export const maxNamePartLength = 100;

/** The person's name, or the placeholder shown instead of it. */
export function fullName(name: PersonName | PersonLabel): string {
  if ('placeholder' in name && name.placeholder) {
    return name.placeholder;
  }
  return [name.firstName, name.middleName, name.lastName].filter(Boolean).join(' ');
}

/** "MR" for Maya Rai, "M" for Maya: the first letters of the first and last names. A placeholder has none. */
export function initials(name: PersonName | PersonLabel): string {
  if ('placeholder' in name && name.placeholder) {
    return '';
  }
  return [name.firstName, name.lastName]
    .map((part) => Array.from(part?.trim() ?? '')[0] ?? '')
    .join('')
    .toLocaleUpperCase();
}
