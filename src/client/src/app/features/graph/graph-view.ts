import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { localNumber } from '../../core/i18n/local-format';
import { DigitsPipe } from '../../core/i18n/local-format.pipes';
import { PeopleService } from '../people/people.service';
import { PersonSummary, fullName } from '../people/person';
import { relationHint } from '../relationships/relationship';
import { problemMessage } from '../people/problem';
import { AddRelatives, AddRelativesResult } from './add-relatives';
import { DeletePerson } from './delete-person';
import { GraphPerson } from './graph';
import { AddRelativeRequest, GraphDiagram } from './graph-diagram';
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
 * navigable list (AC-09.2), and how the person is related to someone else (AC-08.6). The
 * diagram's menu adds parents, children or a spouse to someone (D-032), and the people near them
 * are then loaded again. The menu also deletes a profile you maintain, or anyone's as an
 * administrator (D-034), which then shows as a `Deleted` placeholder. Shown on a person's graph page.
 */
@Component({
  selector: 'app-graph-view',
  imports: [AddRelatives, DeletePerson, DigitsPipe, GraphDiagram, PersonConnections, RouterLink],
  templateUrl: './graph-view.html',
  styleUrl: './graph-view.css',
})
export class GraphView {
  private readonly graph = inject(GraphService);
  private readonly people = inject(PeopleService);
  protected readonly auth = inject(AuthService);

  /** The person at the centre. */
  readonly id = input.required<string>();
  /** The centre is not a person you can see, so a host can choose someone else. */
  readonly notFound = output<void>();

  protected readonly fullName = fullName;
  protected readonly relationHint = relationHint;
  protected readonly generationLabel = generationLabel;
  protected readonly maxShownPeople = maxShownPeople;
  protected readonly separator = $localize`:@@common.listSeparator:, `;
  protected readonly shown = signal<ShownGraph | null>(null);
  /** The first view stopped at the server's limit (D-023). */
  protected readonly truncated = signal(false);
  protected readonly selectedId = signal<string | null>(null);
  /** The person whose relatives are loading. */
  protected readonly loadingAround = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);
  /** The person and relation the add dialog is open for. */
  protected readonly adding = signal<AddRelativeRequest | null>(null);
  /** The person the delete dialog is open for. */
  protected readonly deleting = signal<GraphPerson | null>(null);
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
          ? $localize`:@@graph.everyoneShown:Everyone near ${name}:name: is already shown.`
          : added === 1
            ? $localize`:@@graph.addedOne:Added 1 person near ${name}:name:.`
            : $localize`:@@graph.addedMany:Added ${localNumber(added)}:count: people near ${name}:name:.`,
      );
    } catch (error) {
      this.error.set(problemMessage(error));
    } finally {
      this.loadingAround.set(null);
    }
  }

  protected startAdding(request: AddRelativeRequest): void {
    this.notice.set(null);
    this.error.set(null);
    this.adding.set(request);
  }

  /** Shows the new active links by merging the view around the person (pending ones stay hidden). */
  protected async finishAdding({ changed, message }: AddRelativesResult): Promise<void> {
    const request = this.adding();
    this.adding.set(null);
    if (message) {
      this.notice.set(message);
    }
    if (!changed || !request) {
      return;
    }
    const centreId = this.id();
    try {
      const view = await this.graph.localView(request.person.id);
      const shown = this.shown();
      if (shown && this.id() === centreId) {
        this.shown.set(expand(shown, view).graph);
      }
    } catch (error) {
      this.error.set(problemMessage(error));
    }
  }

  protected startDeleting(person: GraphPerson): void {
    this.notice.set(null);
    this.error.set(null);
    this.deleting.set(person);
  }

  /** Swaps the deleted person for their `Deleted` placeholder; their links stay (PRD 05). */
  protected async finishDeleting(deleted: boolean): Promise<void> {
    const person = this.deleting();
    this.deleting.set(null);
    if (!deleted || !person) {
      return;
    }
    const name = fullName(person);
    const centreId = this.id();
    try {
      const placeholder = await this.people.get(person.id);
      const shown = this.shown();
      if (shown && this.id() === centreId) {
        this.shown.set({
          ...shown,
          people: shown.people.map((row) =>
            row.person.id === person.id
              ? { ...row, person: { ...placeholder, birthSortDate: null } }
              : row,
          ),
        });
      }
      this.notice.set(
        $localize`:@@graph.deleted:Deleted ${name}:name:. They stay in the graph as “Deleted”, with their links.`,
      );
    } catch (error) {
      this.error.set(problemMessage(error));
    }
  }

  private nameOf(personId: string): string {
    const row = this.shown()?.people.find((row) => row.person.id === personId);
    return row ? fullName(row.person) : '';
  }
}
