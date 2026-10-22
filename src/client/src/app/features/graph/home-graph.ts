import { HttpErrorResponse } from '@angular/common/http';
import { Component, PendingTasks, effect, inject, input, signal, untracked } from '@angular/core';
import { Router, RouterLink } from '@angular/router';

import { fullName } from '../people/person';
import { problemMessage } from '../people/problem';
import { GraphDiagram } from './graph-diagram';
import { GraphService } from './graph.service';
import { ShownGraph, expand, fromView, maxShownPeople } from './shown-graph';

/** How many views home loads at once while it fills in the graph. */
const parallelLoads = 4;

/**
 * The home page's family graph (D-024): everyone connected through active links to the people
 * it starts from, up to the most the client keeps on screen, drawn as dots and lines only, with
 * nobody at the centre. It loads the local view of each starting person, then keeps loading
 * around people with relatives not shown, so every read stays bounded (D-023). Selecting someone
 * opens their graph page. Keyboard and screen reader users get the same people as links, visible
 * only while one of them has focus (AC-09.2).
 */
@Component({
  selector: 'app-home-graph',
  imports: [GraphDiagram, RouterLink],
  templateUrl: './home-graph.html',
  styleUrl: './home-graph.css',
})
export class HomeGraph {
  private readonly graph = inject(GraphService);
  private readonly router = inject(Router);
  private readonly pendingTasks = inject(PendingTasks);

  /** The people to start from: the profiles you maintain. */
  readonly from = input.required<string[]>();

  protected readonly fullName = fullName;
  protected readonly shown = signal<ShownGraph | null>(null);
  protected readonly error = signal<string | null>(null);
  private loading = 0;

  constructor() {
    effect(() => {
      const from = this.from();
      // The app is not stable until the graph has filled in.
      untracked(() => this.pendingTasks.run(() => this.load(from)));
    });
  }

  protected open(personId: string): void {
    this.router.navigate(['/people', personId, 'graph']);
  }

  private async load(from: string[]): Promise<void> {
    const run = ++this.loading;
    this.shown.set(null);
    this.error.set(null);
    let shown = null as ShownGraph | null;
    const loaded = new Set<string>();

    // Each view is centred on someone not yet loaded around, so this ends once every starting
    // person is shown and nobody shown has relatives left out, or the graph is full.
    while ((shown?.people.length ?? 0) < maxShownPeople) {
      const ids = new Set<string>(shown?.people.map((row) => row.person.id));
      const next: string[] = [
        ...from.filter((id) => !ids.has(id)),
        ...(shown?.people.filter((row) => row.hasMore).map((row) => row.person.id) ?? []),
      ]
        .filter((id) => !loaded.has(id))
        .slice(0, parallelLoads);
      if (!next.length) {
        return;
      }
      next.forEach((id) => loaded.add(id));
      const views = await Promise.allSettled(next.map((id) => this.graph.localView(id)));
      if (run !== this.loading) {
        return;
      }
      let failure: unknown = null;
      for (const result of views) {
        if (result.status === 'fulfilled') {
          shown = shown ? expand(shown, result.value).graph : fromView(result.value);
        } else if (!(result.reason instanceof HttpErrorResponse && result.reason.status === 404)) {
          // Someone hidden since the last view is skipped; anything else stops the loading.
          failure = result.reason;
        }
      }
      if (shown) {
        // Views are added nearest first, so the furthest people are the ones left out.
        shown = { ...shown, people: shown.people.slice(0, maxShownPeople) };
        this.shown.set(shown);
      }
      if (failure) {
        this.error.set(problemMessage(failure));
        return;
      }
    }
  }
}
