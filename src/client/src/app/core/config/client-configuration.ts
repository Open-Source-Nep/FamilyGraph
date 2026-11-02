import { Language } from '../i18n/language';

/** Instance settings from `GET /api/configuration`. It carries no secrets. */
export interface ClientConfiguration {
  /** `clientId` is the public OAuth client ID the Client sends to Google; set when enabled. */
  google: { enabled: boolean; clientId: string | null };
  /** The languages with translations, and the instance default (D-047). */
  languages: { default: string; available: Language[] };
}
