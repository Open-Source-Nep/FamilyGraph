import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { ConfigurationService } from './configuration.service';

describe('ConfigurationService', () => {
  let service: ConfigurationService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ConfigurationService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads the instance settings from the API', async () => {
    const loading = service.load();
    http.expectOne('/api/configuration').flush({ google: { enabled: true } });
    await loading;

    expect(service.configuration()).toEqual({ google: { enabled: true } });
  });

  it('starts without settings when the server cannot be reached', async () => {
    const loading = service.load();
    http.expectOne('/api/configuration').flush(null, { status: 502, statusText: 'Bad Gateway' });
    await loading;

    expect(service.configuration()).toBeNull();
  });
});
