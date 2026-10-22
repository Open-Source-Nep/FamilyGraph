import { HttpClient } from '@angular/common/http';
import { Service, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { ClientConfiguration } from './client-configuration';

/**
 * The instance settings, loaded once while the app starts (`provideAppInitializer`). The server
 * reads them from its configuration; the Client never reads Google settings itself.
 */
@Service()
export class ConfigurationService {
  private readonly http = inject(HttpClient);
  private readonly current = signal<ClientConfiguration | null>(null);

  /** `null` until loaded, and when the server could not be reached. */
  readonly configuration = this.current.asReadonly();

  async load(): Promise<void> {
    try {
      this.current.set(
        await firstValueFrom(this.http.get<ClientConfiguration>('/api/configuration')),
      );
    } catch {
      // The app still starts; pages that need the settings say the server is unreachable.
      this.current.set(null);
    }
  }
}
