/** A person's name. Only the first name is required (PRD 01). */
export interface PersonName {
  firstName: string;
  middleName: string | null;
  lastName: string | null;
}

/** A profile as the signed-in user sees it, from `GET /api/people/{id}`. */
export interface Person extends PersonName {
  id: string;
  maintainedByYou: boolean;
}

export interface PersonSummary extends PersonName {
  id: string;
}

/** `isTruncated` says the list stopped at the server's limit. */
export interface ProfileList {
  people: PersonSummary[];
  isTruncated: boolean;
}

/** The longest name part the server accepts. */
export const maxNamePartLength = 100;

export function fullName(name: PersonName): string {
  return [name.firstName, name.middleName, name.lastName].filter(Boolean).join(' ');
}

/** "MR" for Maya Rai, "M" for Maya: the first letters of the first and last names. */
export function initials(name: PersonName): string {
  return [name.firstName, name.lastName]
    .map((part) => Array.from(part?.trim() ?? '')[0] ?? '')
    .join('')
    .toLocaleUpperCase();
}
