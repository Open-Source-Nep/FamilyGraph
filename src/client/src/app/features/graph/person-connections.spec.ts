import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { Connections, GraphPerson } from './graph';
import { GraphService } from './graph.service';
import { PersonConnections } from './person-connections';

const person = (id: string, firstName: string): GraphPerson => ({
  id,
  firstName,
  middleName: null,
  lastName: null,
  placeholder: null,
  visibility: 'Public',
  imageId: null,
  maintainedByYou: false,
});
const sita = person('sita', 'Sita');
const maya = person('maya', 'Maya');
const rina = person('rina', 'Rina');

describe('PersonConnections', () => {
  let fixture: ComponentFixture<PersonConnections>;
  let connections: Connections;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: GraphService, useValue: { connections: () => Promise.resolve(connections) } },
      ],
    });
  });

  const find = async () => {
    fixture = TestBed.createComponent(PersonConnections);
    fixture.componentRef.setInput('from', sita);
    fixture.componentRef.setInput('candidates', [sita, maya, rina]);
    await fixture.whenStable();
    const select: HTMLSelectElement = fixture.nativeElement.querySelector('select');
    select.value = select.options[2].value;
    select.dispatchEvent(new Event('change'));
    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));
    await fixture.whenStable();
  };
  const paths = () =>
    [...fixture.nativeElement.querySelectorAll('ol.path')].map((ol) =>
      [...ol.querySelectorAll('li')].map((li) => li.textContent.replace(/\s+/g, ' ').trim()),
    );

  it('offers everyone but the person themselves', async () => {
    connections = { paths: [], morePathsExist: false, searchComplete: true };
    await find();

    const options = [...fixture.nativeElement.querySelectorAll('option')].map((o) => o.textContent);
    expect(options).toEqual(['Choose a person', 'Maya', 'Rina']);
  });

  it('shows the shortest path with adoptive steps named (AC-08.2, AC-08.3, AC-08.7)', async () => {
    connections = {
      paths: [
        {
          start: sita,
          steps: [
            { linkId: 'l1', relation: 'Parent', type: 'Biological', person: maya },
            { linkId: 'l2', relation: 'Child', type: 'Adoptive', person: rina },
          ],
        },
      ],
      morePathsExist: false,
      searchComplete: true,
    };
    await find();

    expect(paths()).toEqual([['Sita', 'parent: Maya', 'adopted child: Rina']]);
    expect(fixture.nativeElement.textContent).not.toContain('more connections');
  });

  it('lists further paths on request (AC-08.6)', async () => {
    const path = (linkId: string) => ({
      start: sita,
      steps: [{ linkId, relation: 'Parent' as const, type: 'Biological' as const, person: rina }],
    });
    connections = { paths: [path('l1'), path('l2')], morePathsExist: true, searchComplete: true };
    await find();

    expect(fixture.nativeElement.textContent).toContain(
      'There are more connections between these people.',
    );
    expect(paths()).toHaveLength(1);

    const more = [...fixture.nativeElement.querySelectorAll('button')].find(
      (b: HTMLButtonElement) => b.textContent?.trim() === 'Show 1 more',
    ) as HTMLButtonElement;
    more.click();
    await fixture.whenStable();

    expect(paths()).toHaveLength(2);
    expect(fixture.nativeElement.textContent).toContain('Only the first 2 connections are listed.');
  });

  it('tells an incomplete search from no connection', async () => {
    connections = { paths: [], morePathsExist: false, searchComplete: false };
    await find();

    expect(fixture.nativeElement.textContent).toContain('within the search limit');
  });
});
