import { HttpErrorResponse } from '@angular/common/http';
import { Component, input, output } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';

import { GraphPerson, LocalView } from './graph';
import { GraphDiagram } from './graph-diagram';
import { GraphService } from './graph.service';
import { HomeGraph } from './home-graph';
import { ShownGraph, maxShownPeople } from './shown-graph';

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
  maintainedByYou: false,
});

describe('HomeGraph', () => {
  let fixture: ComponentFixture<HomeGraph>;
  let views: (id: string) => LocalView;
  let requested: string[];

  beforeEach(() => {
    requested = [];
    // A family where each view reaches one generation: sita → gita → maya → rina.
    const family: Record<string, LocalView> = {
      maya: {
        centreId: 'maya',
        people: [
          { person: person('maya'), generation: 0, hasMore: false },
          { person: person('gita'), generation: 1, hasMore: true },
          { person: person('rina'), generation: -1, hasMore: false },
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
          { person: person('gita'), generation: 0, hasMore: false },
          { person: person('sita'), generation: 1, hasMore: false },
          { person: person('maya'), generation: -1, hasMore: true },
        ],
        links: [
          { id: 'l1', parentId: 'gita', childId: 'maya', type: 'Biological' },
          { id: 'l3', parentId: 'sita', childId: 'gita', type: 'Biological' },
        ],
        isTruncated: false,
      },
    };
    views = (id) => family[id];
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: GraphService,
          useValue: {
            localView: (id: string) => {
              requested.push(id);
              const view = views(id);
              return view
                ? Promise.resolve(view)
                : Promise.reject(new HttpErrorResponse({ status: 404 }));
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

  const show = async (...from: string[]) => {
    fixture = TestBed.createComponent(HomeGraph);
    fixture.componentRef.setInput('from', from.length ? from : ['maya']);
    await fixture.whenStable();
  };
  const diagram = () =>
    fixture.debugElement.query((el) => el.name === 'app-graph-diagram')?.componentInstance as
      StubDiagram | undefined;
  const shownIds = () =>
    diagram()!
      .graph()
      .people.map((row) => row.person.id);

  it('shows everyone connected as dots, loading around people with relatives not shown', async () => {
    await show();

    expect(diagram()!.dots()).toBe(true);
    expect(shownIds()).toEqual(['maya', 'gita', 'rina', 'sita']);
    expect(requested).toEqual(['maya', 'gita']);
    expect(
      diagram()!
        .graph()
        .people.every((row) => !row.hasMore),
    ).toBe(true);
  });

  it('offers everyone shown as links to their graph page (AC-09.2)', async () => {
    await show();

    const links = [...fixture.nativeElement.querySelectorAll('nav a')] as HTMLAnchorElement[];
    expect(links.map((a) => a.textContent?.trim())).toEqual([
      'maya Rai',
      'gita Rai',
      'rina Rai',
      'sita Rai',
    ]);
    expect(links[1].getAttribute('href')).toBe('/people/gita/graph');
  });

  it("opens a person's graph page when they are selected", async () => {
    await show();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    diagram()!.selected.emit('gita');

    expect(navigate).toHaveBeenCalledWith(['/people', 'gita', 'graph']);
  });

  it('stops at the most people the client keeps on screen', async () => {
    // A long line of people, each view reaching five generations below its centre.
    views = (id) => {
      const at = Number(id.slice(1));
      const ids = Array.from({ length: 6 }, (_, i) => at + i);
      return {
        centreId: id,
        people: ids.map((n, i) => ({ person: person(`p${n}`), generation: -i, hasMore: i > 0 })),
        links: ids.slice(1).map((n) => ({
          id: `l${n}`,
          parentId: `p${n - 1}`,
          childId: `p${n}`,
          type: 'Biological' as const,
        })),
        isTruncated: true,
      };
    };
    await show('p0');

    expect(shownIds().length).toBe(maxShownPeople);
    expect(new Set(requested).size).toBe(requested.length);
  });

  it('also shows people not connected to the first profile', async () => {
    const own = views;
    views = (id) =>
      id === 'anil'
        ? {
            centreId: 'anil',
            people: [{ person: person('anil'), generation: 0, hasMore: false }],
            links: [],
            isTruncated: false,
          }
        : own(id);
    await show('maya', 'anil', 'gita');

    expect(shownIds()).toEqual(['maya', 'gita', 'rina', 'anil', 'sita']);
    expect(requested).toEqual(['maya', 'anil', 'gita']);
  });

  it('skips profiles that are gone', async () => {
    await show('gone', 'maya');

    expect(shownIds()).toEqual(['maya', 'gita', 'rina', 'sita']);
    expect(fixture.nativeElement.querySelector('[role="alert"]')).toBeNull();
  });
});
