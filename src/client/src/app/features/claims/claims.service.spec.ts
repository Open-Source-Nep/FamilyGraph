import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { ClaimsService } from './claims.service';

describe('ClaimsService', () => {
  let service: ClaimsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ClaimsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('submits a claim with its note', async () => {
    const submitted = service.submit('p1', 'I am Maya.');
    const request = http.expectOne('/api/people/p1/claims');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ evidence: 'I am Maya.' });
    request.flush({ id: 'c1' });

    expect((await submitted).id).toBe('c1');
  });

  it('reads a profile’s claim state and the claim lists', async () => {
    const status = service.forPerson('p1');
    http.expectOne('/api/people/p1/claims').flush({ claimedByYou: true });
    expect((await status).claimedByYou).toBe(true);

    const toDecide = service.toDecide();
    http.expectOne('/api/claims/to-decide').flush({ claims: [], isTruncated: false });
    expect((await toDecide).claims).toEqual([]);

    const mine = service.mine();
    http.expectOne('/api/claims/mine').flush({ claims: [], isTruncated: false });
    expect((await mine).isTruncated).toBe(false);
  });

  it('posts "This is me", approvals, rejections and withdrawals', async () => {
    const calls: [Promise<void>, string][] = [
      [service.claimAsSelf('p1'), '/api/people/p1/claim-as-self'],
      [service.approve('c1'), '/api/claims/c1/approve'],
      [service.reject('c1'), '/api/claims/c1/reject'],
      [service.withdraw('c1'), '/api/claims/c1/withdraw'],
    ];
    for (const [call, url] of calls) {
      const request = http.expectOne(url);
      expect(request.request.method).toBe('POST');
      request.flush(null, { status: 204, statusText: 'No Content' });
      await call;
    }
  });
});
