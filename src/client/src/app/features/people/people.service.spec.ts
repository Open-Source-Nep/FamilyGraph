import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { PeopleService } from './people.service';
import { Person } from './person';

const maya: Person = {
  id: 'p1',
  firstName: 'Maya',
  middleName: null,
  lastName: null,
  placeholder: null,
  visibility: 'Public',
  maintainedByYou: true,
};

describe('PeopleService', () => {
  let service: PeopleService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(PeopleService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('creates a profile with a POST', async () => {
    const created = service.create({ firstName: 'Maya', middleName: null, lastName: null });
    const request = http.expectOne('/api/people');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ firstName: 'Maya', middleName: null, lastName: null });
    request.flush(maya);

    expect(await created).toEqual(maya);
  });

  it('renames with a PUT to the name', async () => {
    const renamed = service.rename('p1', {
      firstName: 'Maya',
      middleName: 'Devi',
      lastName: 'Rai',
    });
    const request = http.expectOne('/api/people/p1/name');
    expect(request.request.method).toBe('PUT');
    request.flush(null, { status: 204, statusText: 'No Content' });

    await renamed;
  });

  it('sets the visibility with a PUT', async () => {
    const changed = service.setVisibility('p1', 'Private');
    const request = http.expectOne('/api/people/p1/visibility');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ visibility: 'Private' });
    request.flush(null, { status: 204, statusText: 'No Content' });

    await changed;
  });

  it('lists the profiles the user maintains', async () => {
    const list = service.listMaintained();
    http.expectOne('/api/people/maintained').flush({ people: [maya], isTruncated: false });

    expect((await list).people).toHaveLength(1);
  });
});
