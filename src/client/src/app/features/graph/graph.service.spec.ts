import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { Connections, GraphOverview, LocalView, stepLabel } from './graph';
import { GraphService } from './graph.service';

describe('GraphService', () => {
  let service: GraphService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(GraphService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads the local view around a person', async () => {
    const view: LocalView = { centreId: 'p1', people: [], links: [], isTruncated: false };
    const loaded = service.localView('p1');
    http.expectOne({ method: 'GET', url: '/api/people/p1/graph' }).flush(view);

    expect(await loaded).toEqual(view);
  });

  it('loads the overview of everyone connected to your profiles', async () => {
    const overview: GraphOverview = { people: [], links: [], isTruncated: false };
    const loaded = service.overview();
    http.expectOne({ method: 'GET', url: '/api/graph/overview' }).flush(overview);

    expect(await loaded).toEqual(overview);
  });

  it('finds the connections between two people', async () => {
    const connections: Connections = { paths: [], morePathsExist: false, searchComplete: true };
    const found = service.connections('p1', 'p2');
    http.expectOne({ method: 'GET', url: '/api/people/p1/connections/p2' }).flush(connections);

    expect(await found).toEqual(connections);
  });
});

describe('stepLabel', () => {
  it('names adoptive steps (AC-08.7)', () => {
    expect(stepLabel({ relation: 'Parent', type: 'Biological' })).toBe('parent');
    expect(stepLabel({ relation: 'Child', type: 'Biological' })).toBe('child');
    expect(stepLabel({ relation: 'Parent', type: 'Adoptive' })).toBe('adoptive parent');
    expect(stepLabel({ relation: 'Child', type: 'Adoptive' })).toBe('adopted child');
  });
});
