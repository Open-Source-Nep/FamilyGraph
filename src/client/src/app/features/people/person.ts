/** A person's name. Only the first name is required (PRD 01). */
export interface PersonName {
  firstName: string;
  middleName: string | null;
  lastName: string | null;
}

/** Public profiles show their name to signed-in users; Private ones only to their maintainer and administrators (PRD 05). */
export type Visibility = 'Public' | 'Private';

/** What you see instead of a profile hidden from you. It carries no name or details (PRD 05). */
export type Placeholder = 'Private';

/** A person as the server lets you see them: a profile hidden from you has a `placeholder` and no name. */
export interface PersonLabel {
  firstName: string | null;
  middleName: string | null;
  lastName: string | null;
  placeholder: Placeholder | null;
  visibility: Visibility;
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
}

/** `isTruncated` says the list stopped at the server's limit. */
export interface ProfileList {
  people: PersonSummary[];
  isTruncated: boolean;
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
