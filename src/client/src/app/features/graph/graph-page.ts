import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

import { GraphView } from './graph-view';

/** The family graph centred on one person, from their profile or "Centre the view here". */
@Component({
  selector: 'app-graph-page',
  imports: [GraphView, RouterLink],
  template: `
    <section class="panel graph-panel">
      <app-graph-view [id]="id()" />
      <p><a [routerLink]="['/people', id()]">Back to the profile</a></p>
    </section>
  `,
})
export class GraphPage {
  /** The `:id` route parameter: the person at the centre. */
  readonly id = input.required<string>();
}
