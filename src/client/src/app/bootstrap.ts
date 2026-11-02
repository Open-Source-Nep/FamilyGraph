import { bootstrapApplication } from '@angular/platform-browser';

import { App } from './app';
import { appConfig } from './app.config';
import { Startup } from './core/i18n/startup';

/** Starts the app once the translations are loaded (`main.ts`). */
export function bootstrap(startup: Startup) {
  return bootstrapApplication(App, appConfig(startup));
}
