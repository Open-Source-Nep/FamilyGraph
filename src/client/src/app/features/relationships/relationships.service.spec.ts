import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import {
  CreatedLink,
  DirectRelatives,
  LinkConsent,
  LinkConsentList,
  relationHint,
  relationWord,
} from './relationship';
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

  it('deletes a link with a DELETE', async () => {
    const deleted = service.delete('l1');
    const request = http.expectOne('/api/relationships/l1');
    expect(request.request.method).toBe('DELETE');
    request.flush(null, { status: 204, statusText: 'No Content' });
    await deleted;
  });
});

describe('relationWord', () => {
  it('uses gendered words where the gender is known (AC-08.8)', () => {
    expect(relationWord('parent', 'Female', 'Biological')).toBe('mother');
    expect(relationWord('parent', 'Male', 'Adoptive')).toBe('adoptive father');
    expect(relationWord('child', 'Male', 'Biological')).toBe('son');
    expect(relationWord('child', 'Female', 'Adoptive')).toBe('adopted daughter');
  });

  it('says parent or child for Other, not set or hidden (AC-08.8)', () => {
    expect(relationWord('parent', 'Other', 'Biological')).toBe('parent');
    expect(relationWord('child', null, 'Adoptive')).toBe('adopted child');
  });

  it('gives no hint for a plain parent or child', () => {
    expect(relationHint('parent', null, 'Biological')).toBeNull();
    expect(relationHint('child', 'Male', 'Biological')).toBe('son');
  });
});
