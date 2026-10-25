import { NgTemplateOutlet } from '@angular/common';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { fullName } from '../people/person';
import { problemMessage } from '../people/problem';
import { Bansawali, BansawaliLine, bansawaliTitle, bansawaliTree } from './bansawali';
import { GraphService } from './graph.service';

/**
 * A person's Bansawali (PRD 08, D-043): their descendants as an outline, generation by
 * generation in birth order, through sons by default. The toggle, kept in the URL as
 * `?line=AllChildren`, follows every child. Spouses sit beside each person and never continue
 * the line. Someone whose children were cut off by the limit links to their own Bansawali. Print
 * leaves out the controls.
 */
@Component({
  selector: 'app-bansawali-page',
  imports: [NgTemplateOutlet, RouterLink],
  templateUrl: './bansawali-page.html',
  styleUrl: './bansawali-page.css',
})
export class BansawaliPage {
  private readonly graph = inject(GraphService);

  /** The `:id` route parameter: the ancestor. */
  readonly id = input.required<string>();
  /** The `line` query parameter; anything but `AllChildren` means sons only. */
  readonly line = input<string | undefined>();

  protected readonly fullName = fullName;
  protected readonly allChildren = computed(() => this.line() === 'AllChildren');
  protected readonly bansawali = signal<Bansawali | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly tree = computed(() => {
    const bansawali = this.bansawali();
    return bansawali ? bansawaliTree(bansawali) : null;
  });
  protected readonly title = computed(() => {
    const root = this.tree();
    return root ? bansawaliTitle(root.row.person) : 'Bansawali';
  });
  protected readonly generations = computed(
    () => Math.max(0, ...(this.bansawali()?.people ?? []).map((row) => row.generation)) + 1,
  );

  constructor() {
    effect(() => {
      const id = this.id();
      const line: BansawaliLine = this.allChildren() ? 'AllChildren' : 'Sons';
      this.error.set(null);
      this.graph.bansawali(id, line).then(
        (bansawali) => {
          if (this.id() === id) {
            this.bansawali.set(bansawali);
          }
        },
        (error: unknown) => {
          this.bansawali.set(null);
          this.error.set(problemMessage(error));
        },
      );
    });
  }

  protected lineParams(allChildren: boolean): { line: BansawaliLine | null } {
    return { line: allChildren ? 'AllChildren' : null };
  }

  protected print(): void {
    window.print();
  }
}
