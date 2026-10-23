import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { handoverUrl } from './handover';
import { HandoversService } from './handovers.service';

describe('HandoversService', () => {
  let service: HandoversService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(HandoversService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('creates a handover link with a POST', async () => {
    const offered = service.offer('p1');
    const request = http.expectOne('/api/people/p1/handover');
    expect(request.request.method).toBe('POST');
    request.flush({ token: 'secret', expiresAt: '2026-11-06T09:00:00Z' });

    expect(await offered).toEqual({ token: 'secret', expiresAt: '2026-11-06T09:00:00Z' });
  });

  it('reads the open link, or null when the server answers 204', async () => {
    const open = service.getOpen('p1');
    http
      .expectOne('/api/people/p1/handover')
      .flush({ createdAt: '2026-10-23T09:00:00Z', expiresAt: '2026-11-06T09:00:00Z' });
    expect(await open).toEqual({
      createdAt: '2026-10-23T09:00:00Z',
      expiresAt: '2026-11-06T09:00:00Z',
    });

    const none = service.getOpen('p1');
    http
      .expectOne('/api/people/p1/handover')
      .flush(null, { status: 204, statusText: 'No Content' });
    expect(await none).toBeNull();
  });

  it('cancels with a DELETE', async () => {
    const cancelled = service.cancel('p1');
    const request = http.expectOne('/api/people/p1/handover');
    expect(request.request.method).toBe('DELETE');
    request.flush(null, { status: 204, statusText: 'No Content' });

    await cancelled;
  });

  it('sends the token in the body to preview and accept', async () => {
    const accepted = service.accept('secret');
    const request = http.expectOne('/api/handovers/accept');
    expect(request.request.body).toEqual({ token: 'secret' });
    request.flush({ personId: 'p1' });
    expect(await accepted).toEqual({ personId: 'p1' });

    const previewed = service.preview('secret');
    const preview = http.expectOne('/api/handovers/preview');
    expect(preview.request.body).toEqual({ token: 'secret' });
    preview.flush({ person: { id: 'p1' }, expiresAt: '2026-11-06T09:00:00Z' });
    expect((await previewed).expiresAt).toBe('2026-11-06T09:00:00Z');
  });

  it('puts the secret in the link fragment', () => {
    expect(handoverUrl('https://family.example.org', 'secret')).toBe(
      'https://family.example.org/handover#secret',
    );
  });
});
