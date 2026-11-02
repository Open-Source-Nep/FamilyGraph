import { provideHttpClient } from '@angular/common/http';
import { ApplicationConfig, LOCALE_ID, provideBrowserGlobalErrorListeners } from '@angular/core';
import {
  provideRouter,
  withComponentInputBinding,
  withNavigationErrorHandler,
} from '@angular/router';

import { routes } from './app.routes';
import { STARTUP_CONFIGURATION } from './core/config/configuration.service';
import { Startup } from './core/i18n/startup';
import { reloadOnStaleChunk } from './core/updates/stale-chunk';

export function appConfig(startup: Startup): ApplicationConfig {
  return {
    providers: [
      provideBrowserGlobalErrorListeners(),
      // A tab left open across a deploy reloads instead of failing on a removed chunk.
      provideRouter(
        routes,
        withComponentInputBinding(),
        withNavigationErrorHandler((error) => reloadOnStaleChunk(error)),
      ),
      // XSRF protection is on by default: the XSRF-TOKEN cookie is sent back as X-XSRF-TOKEN (D-011).
      provideHttpClient(),
      // The instance settings (for example whether Google sign-in is set up) and the language were
      // loaded before the app started (main.ts).
      { provide: STARTUP_CONFIGURATION, useValue: startup.configuration },
      { provide: LOCALE_ID, useValue: startup.localeId },
    ],
  };
}
