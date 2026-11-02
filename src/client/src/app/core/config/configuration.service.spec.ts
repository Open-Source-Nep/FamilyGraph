import { TestBed } from '@angular/core/testing';

import { ConfigurationService, STARTUP_CONFIGURATION } from './configuration.service';

describe('ConfigurationService', () => {
  it('holds the instance settings loaded at startup', () => {
    const settings = {
      google: { enabled: true, clientId: 'client-id' },
      languages: { default: 'en', available: [{ code: 'en', name: 'English' }] },
    };
    TestBed.configureTestingModule({
      providers: [{ provide: STARTUP_CONFIGURATION, useValue: settings }],
    });

    expect(TestBed.inject(ConfigurationService).configuration()).toEqual(settings);
  });

  it('has no settings when the server could not be reached', () => {
    expect(TestBed.inject(ConfigurationService).configuration()).toBeNull();
  });
});
