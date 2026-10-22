import { Component, input, output } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { PeopleService } from '../people/people.service';
import { GraphPerson, LocalView } from './graph';
import { GraphDiagram } from './graph-diagram';
import { GraphView } from './graph-view';
import { GraphService } from './graph.service';
import { ShownGraph } from './shown-graph';

/** Cytoscape needs a canvas, which jsdom lacks; the diagram is checked in the browser. */
@Component({ selector: 'app-graph-diagram', template: '' })
class StubDiagram {
  readonly graph = input.required<ShownGraph>();
  readonly selectedId = input<string | null>(null);
  readonly selected = output<string>();
}

const person = (id: string, firstName: string): GraphPerson => ({
  id,
  firstName,
  middleName: null,
  lastName: 'Rai',
  maintainedByYou: false,
});

describe('GraphView', () => {
  let fixture: ComponentFixture<GraphView>;
  let views: Record<string, LocalView>;

  beforeEach(() => {
    views = {
      maya: {
        centreId: 'maya',
        people: [
          { person: person('maya', 'Maya'), generation: 0, hasMore: false },
          { person: person('gita', 'Gita'), generation: 1, hasMore: true },
          { person: person('rina', 'Rina'), generation: -1, hasMore: false },
        ],
        links: [
          { id: 'l1', parentId: 'gita', childId: 'maya', type: 'Biological' },
          { id: 'l2', parentId: 'maya', childId: 'rina', type: 'Adoptive' },
        ],
        isTruncated: true,
      },
      gita: {
        centreId: 'gita',
        people: [
          { person: person('gita', 'Gita'), generation: 0, hasMore: false },
          { person: person('sita', 'Sita'), generation: 1, hasMore: false },
          { person: person('maya', 'Maya'), generation: -1, hasMore: true },
        ],
        links: [
          { id: 'l1', parentId: 'gita', childId: 'maya', type: 'Biological' },
          { id: 'l3', parentId: 'sita', childId: 'gita', type: 'Biological' },
        ],
        isTruncated: false,
      },
    };
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: GraphService,
          useValue: { localView: (id: string) => Promise.resolve(views[id]) },
        },
        {
          provide: PeopleService,
          useValue: { listMaintained: () => Promise.resolve({ people: [], isTruncated: false }) },
        },
      ],
    });
    TestBed.overrideComponent(GraphView, {
      remove: { imports: [GraphDiagram] },
      add: { imports: [StubDiagram] },
    });
  });

  const show = async () => {
    fixture = TestBed.createComponent(GraphView);
    fixture.componentRef.setInput('id', 'maya');
    await fixture.whenStable();
  };
  const text = () => fixture.nativeElement.textContent as string;
  const button = (label: string) =>
    [...fixture.nativeElement.querySelectorAll('button')].find(
      (b: HTMLButtonElement) => b.textContent?.trim() === label,
    ) as HTMLButtonElement | undefined;

  it('shows a local view as a list by generation (AC-08.4, AC-09.2)', async () => {
    await show();

    expect(fixture.nativeElement.querySelector('h1').textContent).toBe(
      'Family graph around Maya Rai',
    );
    const headings = [...fixture.nativeElement.querySelectorAll('h3')].map((h) => h.textContent);
    expect(headings).toEqual(['1 generation above', 'Same generation', '1 generation below']);
    expect(text()).toContain('More relatives are in reach');
    expect(text()).toContain('(adopted child)');
  });

  it('remembers its centre for the home page', async () => {
    localStorage.clear();
    await show();

    expect(localStorage.getItem('familygraph.graphCentre')).toBe('maya');
  });

  it('loads more around an edge person (AC-08.5)', async () => {
    await show();
    expect(button('Load more around Maya Rai')).toBeUndefined();

    button('Load more around Gita Rai')!.click();
    await fixture.whenStable();

    expect(text()).toContain('Added 1 person near Gita Rai.');
    expect(text()).toContain('2 generations above');
    expect(text()).toContain('Sita Rai');
    expect(button('Load more around Gita Rai')).toBeUndefined();
  });
});
