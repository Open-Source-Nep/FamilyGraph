import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { AdminService } from './admin.service';

describe('AdminService', () => {
  let service: AdminService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AdminService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists accounts and the profiles of one', async () => {
    const accounts = service.listAccounts();
    http.expectOne('/api/admin/accounts').flush({ accounts: [], isTruncated: false });
    expect(await accounts).toEqual({ accounts: [], isTruncated: false });

    const profiles = service.listProfiles('a 1');
    http.expectOne('/api/admin/accounts/a%201/profiles').flush({ people: [], isTruncated: false });
    expect(await profiles).toEqual({ people: [], isTruncated: false });
  });

  it('transfers one profile with a POST naming the receiving account', async () => {
    const done = service.transferProfile('p1', 'a2');
    const request = http.expectOne('/api/admin/people/p1/transfer');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ toAccountId: 'a2' });
    request.flush(null, { status: 204, statusText: 'No Content' });
    await done;
  });

  it('transfers all profiles of an account and returns how many moved', async () => {
    const done = service.transferAll('a1', 'a2');
    const request = http.expectOne('/api/admin/accounts/a1/transfer');
    expect(request.request.body).toEqual({ toAccountId: 'a2' });
    request.flush({ transferred: 3 });
    expect(await done).toEqual({ transferred: 3 });
  });
});
