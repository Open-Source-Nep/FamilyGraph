import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DataImport } from './data-import';

describe('DataImport', () => {
  let fixture: ComponentFixture<DataImport>;
  let http: HttpTestingController;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    fixture = TestBed.createComponent(DataImport);
    http = TestBed.inject(HttpTestingController);
    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  const element = () => fixture.nativeElement as HTMLElement;
  const button = () => element().querySelector('button') as HTMLButtonElement;

  async function chooseFile(): Promise<File> {
    const file = new File(['{"format":"familygraph-export"}'], 'export.json', {
      type: 'application/json',
    });
    const input = element().querySelector('input[type="file"]') as HTMLInputElement;
    Object.defineProperty(input, 'files', { value: [file] });
    input.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    return file;
  }

  it('posts the chosen file and shows what was imported', async () => {
    expect(button().disabled).toBe(true);
    const file = await chooseFile();

    button().click();
    const request = http.expectOne('/api/import');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toBe(file);
    expect(request.request.headers.get('Content-Type')).toBe('application/json');
    request.flush({ people: 3, links: 1, marriages: 0, matchedAccounts: 1, assignedToYou: 1 });
    await fixture.whenStable();

    const status = element().querySelector('[role="status"]')?.textContent ?? '';
    expect(status).toContain('Imported 3 profiles, 1 parent–child links and 0 marriages.');
    expect(status).toContain('You now maintain 1 profiles');
  });

  it('lists the problems of a refused file', async () => {
    await chooseFile();

    button().click();
    http.expectOne('/api/import').flush(
      {
        code: 'import.invalid_file',
        problems: [
          {
            path: 'people[1]',
            code: 'person.first_name_required',
            message: 'A person must have a first name.',
          },
        ],
        moreProblems: true,
      },
      { status: 422, statusText: 'Unprocessable Entity' },
    );
    await fixture.whenStable();

    const alert = element().querySelector('[role="alert"]')?.textContent ?? '';
    expect(alert).toContain('The file cannot be imported. Nothing was saved.');
    expect(alert).toContain('people[1]: A person must have a first name.');
    expect(alert).toContain('There are more problems');
  });

  it('explains that an instance with profiles cannot import', async () => {
    await chooseFile();

    button().click();
    http
      .expectOne('/api/import')
      .flush({ code: 'import.instance_not_empty' }, { status: 409, statusText: 'Conflict' });
    await fixture.whenStable();

    expect(element().querySelector('[role="alert"]')?.textContent).toContain(
      'This instance already has profiles.',
    );
  });
});
