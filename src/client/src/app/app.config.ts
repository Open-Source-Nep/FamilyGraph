import { provideHttpClient } from '@angular/common/http';
import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';

import { routes } from './app.routes';
import { ConfigurationService } from './core/config/configuration.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    // XSRF protection is on by default: the XSRF-TOKEN cookie is sent back as X-XSRF-TOKEN (D-011).
    provideHttpClient(),
    // The instance settings (for example whether Google sign-in is set up) come from the server.
    provideAppInitializer(() => inject(ConfigurationService).load()),
  ],
};
