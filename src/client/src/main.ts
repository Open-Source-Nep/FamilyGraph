/// <reference types="@angular/localize" />

import { loadStartup } from './app/core/i18n/startup';

// `$localize` translates messages as their modules load, so the app is imported only once the
// chosen language's translations are loaded (D-047).
loadStartup()
  .then(async (startup) => (await import('./app/bootstrap')).bootstrap(startup))
  .catch((err) => console.error(err));
