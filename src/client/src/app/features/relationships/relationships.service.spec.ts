import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { CreatedLink, DirectRelatives } from './relationship';
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
});
