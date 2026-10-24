import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { PeopleService } from '../people/people.service';
import { PersonSummary, fullName } from '../people/person';
import { relationHint } from '../relationships/relationship';
import { problemMessage } from '../people/problem';
import { GraphDiagram } from './graph-diagram';
import { GraphService } from './graph.service';
import { ConnectionCandidate, PersonConnections } from './person-connections';
import {
  ShownGraph,
  byGeneration,
  expand,
  fromView,
  generationLabel,
  maxShownPeople,
} from './shown-graph';

/**
 * The family graph around a person (PRD 08, #23): a tree diagram of the people nearest to them,
 * expandable from people with relatives not shown (AC-08.4, AC-08.5), the same people as a
 * navigable list (AC-09.2), and how the person is related to someone else (AC-08.6). Shown on
 * a person's graph page.
 */
@Component({
  selector: 'app-graph-view',
  imports: [GraphDiagram, PersonConnections, RouterLink],
  templateUrl: './graph-view.html',
  styleUrl: './graph-view.css',
})
export class GraphView {
  private readonly graph = inject(GraphService);
  private readonly people = inject(PeopleService);

  /** The person at the centre. */
  readonly id = input.required<string>();
  /** The centre is not a person you can see, so a host can choose someone else. */
  readonly notFound = output<void>();

  protected readonly fullName = fullName;
  protected readonly relationHint = relationHint;
  protected readonly generationLabel = generationLabel;
  protected readonly maxShownPeople = maxShownPeople;
  protected readonly shown = signal<ShownGraph | null>(null);
  /** The first view stopped at the server's limit (D-023). */
  protected readonly truncated = signal(false);
  protected readonly selectedId = signal<string | null>(null);
  /** The person whose relatives are loading. */
  protected readonly loadingAround = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);
  private readonly maintained = signal<PersonSummary[]>([]);

  protected readonly centre = computed(() => {
    const shown = this.shown();
    return shown?.people.find((row) => row.person.id === shown.centreId)?.person ?? null;
  });
  protected readonly selected = computed(
    () => this.shown()?.people.find((row) => row.person.id === this.selectedId()) ?? null,
  );
  protected readonly generations = computed(() => {
    const shown = this.shown();
    return shown ? byGeneration(shown) : [];
  });
  protected readonly full = computed(() => (this.shown()?.people.length ?? 0) >= maxShownPeople);
  protected readonly candidates = computed<ConnectionCandidate[]>(() => {
    const shown = this.shown()?.people.map((row) => row.person) ?? [];
    const ids = new Set(shown.map((person) => person.id));
    return [...shown, ...this.maintained().filter((person) => !ids.has(person.id))];
  });

  constructor() {
    effect(() => {
      const id = this.id();
      this.shown.set(null);
      this.selectedId.set(null);
      this.notice.set(null);
      this.error.set(null);
      this.graph.localView(id).then(
        (view) => {
          if (this.id() === id) {
            this.shown.set(fromView(view));
            this.truncated.set(view.isTruncated);
          }
        },
        (error: unknown) => {
          if (error instanceof HttpErrorResponse && error.status === 404) {
            this.notFound.emit();
          }
          this.error.set(problemMessage(error));
        },
      );
    });
    // Only to offer as the other person in a connection search; the graph works without it.
    this.people.listMaintained().then(
      (list) => this.maintained.set(list.people),
      () => undefined,
    );
  }

  /** Loads the people nearest to an edge person into the view (AC-08.5). */
  protected async loadMore(personId: string): Promise<void> {
    const centreId = this.id();
    const name = this.nameOf(personId);
    this.loadingAround.set(personId);
    this.notice.set(null);
    this.error.set(null);
    try {
      const view = await this.graph.localView(personId);
      const shown = this.shown();
      if (!shown || this.id() !== centreId) {
        return;
      }
      const { graph, added } = expand(shown, view);
      this.shown.set(graph);
      this.notice.set(
        added === 0
          ? `Everyone near ${name} is already shown.`
          : `Added ${added} ${added === 1 ? 'person' : 'people'} near ${name}.`,
      );
    } catch (error) {
      this.error.set(problemMessage(error));
    } finally {
      this.loadingAround.set(null);
    }
  }

  private nameOf(personId: string): string {
    const row = this.shown()?.people.find((row) => row.person.id === personId);
    return row ? fullName(row.person) : '';
  }
}
