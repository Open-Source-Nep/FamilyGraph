import { Component, input, output } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { signal } from '@angular/core';

import { AuthService } from '../../core/auth/auth.service';
import { PeopleService } from '../people/people.service';
import { GraphPerson, LocalView } from './graph';
import { AddRelatives, AddRelativesResult } from './add-relatives';
import { DeletePerson } from './delete-person';
import { AddRelation, AddRelativeRequest, GraphDiagram } from './graph-diagram';
import { GraphView } from './graph-view';
import { GraphService } from './graph.service';
import { ShownGraph } from './shown-graph';

/** Cytoscape needs a canvas, which jsdom lacks; the diagram is checked in the browser. */
@Component({ selector: 'app-graph-diagram', template: '' })
class StubDiagram {
  readonly graph = input.required<ShownGraph>();
  readonly selectedId = input<string | null>(null);
  readonly addMenu = input(false);
  readonly administrator = input(false);
  readonly selected = output<string>();
  readonly addRelative = output<AddRelativeRequest>();
  readonly deletePerson = output<GraphPerson>();
}

@Component({ selector: 'app-delete-person', template: '' })
class StubDeletePerson {
  readonly person = input.required<GraphPerson>();
  readonly closed = output<boolean>();
}

@Component({ selector: 'app-add-relatives', template: '' })
class StubAddRelatives {
  readonly person = input.required<GraphPerson>();
  readonly relation = input.required<AddRelation>();
  readonly closed = output<AddRelativesResult>();
}

const person = (id: string, firstName: string): GraphPerson => ({
  id,
  firstName,
  middleName: null,
  lastName: 'Rai',
  placeholder: null,
  visibility: 'Public',
  imageId: null,
  gender: null,
  maintainedByYou: false,
  description: null,
});

describe('GraphView', () => {
  let fixture: ComponentFixture<GraphView>;
  let views: Record<string, LocalView>;
  let administrator: ReturnType<typeof signal<boolean>>;

  beforeEach(() => {
    administrator = signal(false);
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
        marriages: [],
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
        marriages: [],
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
          useValue: {
            listMaintained: () => Promise.resolve({ people: [], isTruncated: false }),
            get: (id: string) =>
              Promise.resolve({
                ...person(id, ''),
                firstName: null,
                lastName: null,
                placeholder: 'Deleted',
              }),
          },
        },
        { provide: AuthService, useValue: { isAdministrator: administrator } },
      ],
    });
    TestBed.overrideComponent(GraphView, {
      remove: { imports: [GraphDiagram, AddRelatives, DeletePerson] },
      add: { imports: [StubDiagram, StubAddRelatives, StubDeletePerson] },
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
  const diagram = () =>
    fixture.debugElement.query((el) => el.name === 'app-graph-diagram')
      .componentInstance as StubDiagram;

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

  it('shows the person selected in the diagram', async () => {
    await show();

    diagram().selected.emit('gita');
    await fixture.whenStable();

    expect(diagram().selectedId()).toBe('gita');
    expect(fixture.nativeElement.querySelector('.selected').textContent).toContain('Gita Rai');
  });

  it('adds relatives from the diagram menu and shows the new links (AC-08.10)', async () => {
    await show();
    expect(diagram().addMenu()).toBe(true);
    const rina = person('rina', 'Rina');

    diagram().addRelative.emit({ person: rina, relation: 'child' });
    await fixture.whenStable();
    const dialog = () =>
      fixture.debugElement.query((el) => el.name === 'app-add-relatives')?.componentInstance as
        StubAddRelatives | undefined;
    expect(dialog()?.relation()).toBe('child');
    expect(dialog()?.person()).toBe(rina);

    views['rina'] = {
      centreId: 'rina',
      people: [
        { person: rina, generation: 0, hasMore: false },
        { person: person('kiran', 'Kiran'), generation: -1, hasMore: false },
      ],
      links: [{ id: 'l4', parentId: 'rina', childId: 'kiran', type: 'Biological' }],
      marriages: [],
      isTruncated: false,
    };
    dialog()!.closed.emit({ changed: true, message: 'Linked 1 person to Rina Rai.' });
    await fixture.whenStable();

    expect(dialog()).toBeUndefined();
    expect(text()).toContain('Linked 1 person to Rina Rai.');
    expect(text()).toContain('Kiran Rai');
    expect(text()).toContain('2 generations below');
  });

  it('tells the diagram whether you are an administrator (AC-08.14)', async () => {
    administrator.set(true);
    await show();

    expect(diagram().administrator()).toBe(true);
  });

  it('deletes from the diagram menu after confirming and shows the placeholder (AC-08.14)', async () => {
    await show();
    const gita = views['maya'].people[1].person;

    diagram().deletePerson.emit(gita);
    await fixture.whenStable();
    const dialog = () =>
      fixture.debugElement.query((el) => el.name === 'app-delete-person')?.componentInstance as
        StubDeletePerson | undefined;
    expect(dialog()?.person()).toBe(gita);

    dialog()!.closed.emit(true);
    await fixture.whenStable();

    expect(dialog()).toBeUndefined();
    expect(text()).toContain('Deleted Gita Rai. They stay in the graph as “Deleted”');
    const list = [...fixture.nativeElement.querySelectorAll('.graph-people')]
      .map((ul) => ul.textContent)
      .join(' ');
    expect(list).not.toContain('Gita');
    expect(list).toContain('Parents: Deleted');
    const row = diagram()
      .graph()
      .people.find((row) => row.person.id === 'gita');
    expect(row?.person.placeholder).toBe('Deleted');
    expect(diagram().graph().links).toHaveLength(2);
  });

  it('keeps the view as it was when the deletion is cancelled', async () => {
    await show();

    diagram().deletePerson.emit(views['maya'].people[1].person);
    await fixture.whenStable();
    (
      fixture.debugElement.query((el) => el.name === 'app-delete-person')
        .componentInstance as StubDeletePerson
    ).closed.emit(false);
    await fixture.whenStable();

    expect(text()).toContain('Gita Rai');
    expect(text()).not.toContain('Deleted');
  });
});
