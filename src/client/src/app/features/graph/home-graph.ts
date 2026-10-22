import { Component, PendingTasks, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';

import { fullName } from '../people/person';
import { problemMessage } from '../people/problem';
import { GraphDiagram } from './graph-diagram';
import { GraphService } from './graph.service';
import { ShownGraph, fromOverview } from './shown-graph';

/**
 * The home page's family graph (D-024): everyone connected through active links to the profiles
 * you maintain, from one bounded request (D-023), drawn as dots and lines with nobody at the
 * centre. Hovering a dot shows the person's name; selecting it opens their graph page. Keyboard
 * and screen reader users get the same people as links, visible only while one of them has focus
 * (AC-09.2).
 */
@Component({
  selector: 'app-home-graph',
  imports: [GraphDiagram, RouterLink],
  templateUrl: './home-graph.html',
  styleUrl: './home-graph.css',
})
export class HomeGraph {
  private readonly router = inject(Router);

  protected readonly fullName = fullName;
  protected readonly shown = signal<ShownGraph | null>(null);
  protected readonly error = signal<string | null>(null);

  constructor() {
    const graph = inject(GraphService);
    // The app is not stable until the graph has loaded.
    inject(PendingTasks).run(() =>
      graph.overview().then(
        (overview) => this.shown.set(fromOverview(overview)),
        (error: unknown) => this.error.set(problemMessage(error)),
      ),
    );
  }

  protected open(personId: string): void {
    this.router.navigate(['/people', personId, 'graph']);
  }
}
