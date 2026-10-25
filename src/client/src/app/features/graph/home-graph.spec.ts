import { Component, input, output } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';

import { GraphOverview, GraphPerson } from './graph';
import { GraphDiagram } from './graph-diagram';
import { GraphService } from './graph.service';
import { HomeGraph } from './home-graph';
import { ShownGraph } from './shown-graph';

/** Cytoscape needs a canvas, which jsdom lacks; the diagram is checked in the browser. */
@Component({ selector: 'app-graph-diagram', template: '' })
class StubDiagram {
  readonly graph = input.required<ShownGraph>();
  readonly dots = input(false);
  readonly selected = output<string>();
}

const person = (id: string): GraphPerson => ({
  id,
  firstName: id,
  middleName: null,
  lastName: 'Rai',
  placeholder: null,
  visibility: 'Public',
  imageId: null,
  gender: null,
  maintainedByYou: false,
  description: null,
  birthSortDate: null,
});

describe('HomeGraph', () => {
  let fixture: ComponentFixture<HomeGraph>;
  let overview: () => Promise<GraphOverview>;
  let requests: number;

  beforeEach(() => {
    requests = 0;
    overview = () =>
      Promise.resolve({
        people: [person('maya'), person('gita'), person('rina')],
        links: [
          { id: 'l1', parentId: 'gita', childId: 'maya', type: 'Biological', birthOrder: null },
          { id: 'l2', parentId: 'maya', childId: 'rina', type: 'Adoptive', birthOrder: null },
        ],
        marriages: [],
        isTruncated: false,
      });
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: GraphService,
          useValue: {
            overview: () => {
              requests++;
              return overview();
            },
          },
        },
      ],
    });
    TestBed.overrideComponent(HomeGraph, {
      remove: { imports: [GraphDiagram] },
      add: { imports: [StubDiagram] },
    });
  });

  const show = async () => {
    fixture = TestBed.createComponent(HomeGraph);
    await fixture.whenStable();
  };
  const diagram = () =>
    fixture.debugElement.query((el) => el.name === 'app-graph-diagram')?.componentInstance as
      StubDiagram | undefined;

  it('shows everyone connected as dots with no centre, from one request', async () => {
    await show();

    expect(requests).toBe(1);
    expect(diagram()!.dots()).toBe(true);
    const graph = diagram()!.graph();
    expect(graph.centreId).toBeNull();
    expect(graph.people.map((row) => row.person.id)).toEqual(['maya', 'gita', 'rina']);
    expect(graph.people.every((row) => !row.hasMore)).toBe(true);
    expect(graph.links.length).toBe(2);
  });

  it('offers everyone shown as links to their graph page (AC-09.2)', async () => {
    await show();

    const links = [...fixture.nativeElement.querySelectorAll('nav a')] as HTMLAnchorElement[];
    expect(links.map((a) => a.textContent?.trim())).toEqual(['maya Rai', 'gita Rai', 'rina Rai']);
    expect(links[1].getAttribute('href')).toBe('/people/gita/graph');
  });

  it("opens a person's graph page when they are selected", async () => {
    await show();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    diagram()!.selected.emit('gita');

    expect(navigate).toHaveBeenCalledWith(['/people', 'gita', 'graph']);
  });

  it('says when the graph could not be loaded', async () => {
    overview = () => Promise.reject(new Error('offline'));
    await show();

    expect(diagram()).toBeUndefined();
    expect(fixture.nativeElement.querySelector('[role="alert"]')).not.toBeNull();
  });
});
