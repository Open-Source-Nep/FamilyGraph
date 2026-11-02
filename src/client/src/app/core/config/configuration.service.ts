import { InjectionToken, Service, inject, signal } from '@angular/core';

import { ClientConfiguration } from './client-configuration';

/** The instance settings `main.ts` loaded before the app started; `null` when unreachable. */
export const STARTUP_CONFIGURATION = new InjectionToken<ClientConfiguration | null>(
  'STARTUP_CONFIGURATION',
  { factory: () => null },
);

/**
 * The instance settings, loaded once before the app starts, because they choose the language
 * (D-047). The server reads them from its configuration, including the public Google client ID.
 */
@Service()
export class ConfigurationService {
  /** `null` when the server could not be reached; pages that need it say so. */
  readonly configuration = signal(inject(STARTUP_CONFIGURATION)).asReadonly();
}
