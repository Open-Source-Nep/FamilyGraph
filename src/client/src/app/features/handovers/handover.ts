import { Person } from '../people/person';

/** A new handover link's secret. The server keeps only its hash, so it is shown once. */
export interface CreatedHandover {
  token: string;
  expiresAt: string;
}

/** The profile's open handover link, which may have expired. */
export interface OpenHandover {
  createdAt: string;
  expiresAt: string;
}

/** The profile a link hands over, as you may see it now, and when the link expires. */
export interface HandoverPreview {
  person: Person;
  expiresAt: string;
}

export interface AcceptedHandover {
  personId: string;
}

/** The Client route a handover link opens. The secret is the URL fragment, so it never reaches the server's logs. */
export const handoverPath = '/handover';

export function handoverUrl(origin: string, token: string): string {
  return `${origin}${handoverPath}#${token}`;
}
