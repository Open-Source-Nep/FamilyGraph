import { provideHttpClient } from '@angular/common/http';
import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import {
  provideRouter,
  withComponentInputBinding,
  withNavigationErrorHandler,
} from '@angular/router';

import { routes } from './app.routes';
import { ConfigurationService } from './core/config/configuration.service';
import { reloadOnStaleChunk } from './core/updates/stale-chunk';

export const appConfig: ApplicationConfig = {
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
    // The instance settings (for example whether Google sign-in is set up) come from the server.
    provideAppInitializer(() => inject(ConfigurationService).load()),
  ],
};
