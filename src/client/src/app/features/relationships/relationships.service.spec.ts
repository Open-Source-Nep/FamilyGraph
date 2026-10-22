import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { CreatedLink, DirectRelatives, LinkConsent, LinkConsentList } from './relationship';
import { RelationshipsService } from './relationships.service';

describe('RelationshipsService', () => {
  let service: RelationshipsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(RelationshipsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('creates a link with a POST', async () => {
    const link: CreatedLink = {
      id: 'l1',
      parentId: 'p1',
      childId: 'p2',
      type: 'Adoptive',
      isActive: true,
    };
    const created = service.create({ parentId: 'p1', childId: 'p2', type: 'Adoptive' });
    const request = http.expectOne('/api/relationships');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ parentId: 'p1', childId: 'p2', type: 'Adoptive' });
    request.flush(link);

    expect(await created).toEqual(link);
  });

  it('lists the direct relatives of a person', async () => {
    const relatives: DirectRelatives = {
      parents: [],
      parentsTruncated: false,
      children: [],
      childrenTruncated: false,
    };
    const listed = service.listDirectRelatives('p1');
    http.expectOne('/api/people/p1/relatives').flush(relatives);

    expect(await listed).toEqual(relatives);
  });

  it('lists the inactive links of your profiles', async () => {
    const list: LinkConsentList = { links: [], isTruncated: false };
    const listed = service.listInactive();
    http.expectOne('/api/relationships/inactive').flush(list);

    expect(await listed).toEqual(list);
  });

  it('approves and disagrees for one endpoint with a POST', async () => {
    const link = { id: 'l1', isActive: true } as LinkConsent;
    const approved = service.approve('l1', 'Parent');
    const approval = http.expectOne('/api/relationships/l1/approve');
    expect(approval.request.method).toBe('POST');
    expect(approval.request.body).toEqual({ endpoint: 'Parent' });
    approval.flush(link);
    expect(await approved).toEqual(link);

    const disagreed = service.disagree('l1', 'Child');
    const disagreement = http.expectOne('/api/relationships/l1/disagree');
    expect(disagreement.request.body).toEqual({ endpoint: 'Child' });
    disagreement.flush(link);
    expect(await disagreed).toEqual(link);
  });
});
