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
  imageId: null,
  gender: null,
  maintainedByYou: true,
  description: null,
  birthDate: null,
  birthDateShown: false,
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

  it('searches people by name with the text as a query parameter', async () => {
    const found = service.search('maya rai');
    const request = http.expectOne((req) => req.url === '/api/people/search');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('q')).toBe('maya rai');
    request.flush({ people: [maya], isTruncated: true });
    expect(await found).toEqual({ people: [maya], isTruncated: true });
  });

  it('creates a profile with a POST', async () => {
    const created = service.create({
      name: { firstName: 'Maya', middleName: null, lastName: null },
      gender: 'Female',
      description: 'Teacher',
      birthDate: { calendar: 'Bs', year: 2009, month: 1, day: null },
      birthDateShown: false,
    });
    const request = http.expectOne('/api/people');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      firstName: 'Maya',
      middleName: null,
      lastName: null,
      gender: 'Female',
      description: 'Teacher',
      birthDate: { calendar: 'Bs', year: 2009, month: 1, day: null },
      birthDateShown: false,
    });
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

  it('sets and clears the gender with a PUT', async () => {
    const changed = service.setGender('p1', null);
    const request = http.expectOne('/api/people/p1/gender');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ gender: null });
    request.flush(null, { status: 204, statusText: 'No Content' });

    await changed;
  });

  it('sets the visibility with a PUT', async () => {
    const changed = service.setVisibility('p1', 'Private');
    const request = http.expectOne('/api/people/p1/visibility');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ visibility: 'Private' });
    request.flush(null, { status: 204, statusText: 'No Content' });

    await changed;
  });

  it('deletes a profile with a DELETE', async () => {
    const done = service.delete('p1');
    const request = http.expectOne('/api/people/p1');
    expect(request.request.method).toBe('DELETE');
    request.flush(null, { status: 204, statusText: 'No Content' });

    await done;
  });

  it('lists the profiles the user maintains', async () => {
    const list = service.listMaintained();
    http.expectOne('/api/people/maintained').flush({ people: [maya], isTruncated: false });

    expect((await list).people).toHaveLength(1);
  });

  it('uploads a photo as the raw request body', async () => {
    const photo = new Blob(['jpeg'], { type: 'image/jpeg' });
    const stored = service.setImage('p1', photo);
    const request = http.expectOne('/api/people/p1/image');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toBe(photo);
    expect(request.request.headers.get('Content-Type')).toBe('image/jpeg');
    request.flush({ imageId: 'i1', gender: null });

    expect(await stored).toBe('i1');
  });

  it('removes the photo with a DELETE', async () => {
    const removed = service.removeImage('p1');
    const request = http.expectOne('/api/people/p1/image');
    expect(request.request.method).toBe('DELETE');
    request.flush(null, { status: 204, statusText: 'No Content' });

    await removed;
  });
});
