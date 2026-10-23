import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { PhotoLoader } from './photo-loader.service';

describe('PhotoLoader', () => {
  let loader: PhotoLoader;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    loader = TestBed.inject(PhotoLoader);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads a photo once as a data URL', async () => {
    const first = loader.dataUrl({ id: 'p1', imageId: 'i1' });
    const second = loader.dataUrl({ id: 'p1', imageId: 'i1' });
    http.expectOne('/api/people/p1/image?v=i1').flush(new Blob(['abc'], { type: 'image/jpeg' }));

    expect(await first).toBe('data:image/jpeg;base64,YWJj');
    expect(await second).toBe('data:image/jpeg;base64,YWJj');
  });

  it('has no photo without an image or when the server refuses it', async () => {
    expect(await loader.dataUrl({ id: 'p1', imageId: null })).toBeNull();

    const hidden = loader.dataUrl({ id: 'p2', imageId: 'i2' });
    http
      .expectOne('/api/people/p2/image?v=i2')
      .flush(new Blob(), { status: 404, statusText: 'Not Found' });

    expect(await hidden).toBeNull();
  });
});
