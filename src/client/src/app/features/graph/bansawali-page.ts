import { NgTemplateOutlet } from '@angular/common';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { PersonAvatar } from '../people/person-avatar';
import { fullName } from '../people/person';
import { problemMessage } from '../people/problem';
import {
  Bansawali,
  BansawaliEntry,
  BansawaliLine,
  bansawaliSections,
  bansawaliTitle,
  bansawaliTree,
  sectionGenerations,
} from './bansawali';
import { GraphService } from './graph.service';

/**
 * A person's Bansawali (PRD 08, D-043): their descendants as an outline, generation by
 * generation in birth order, through sons by default. The toggle, kept in the URL as
 * `?line=AllChildren`, follows every child. Spouses sit beside each person and never continue
 * the line. Someone whose children were cut off by the limit links to their own Bansawali. Each
 * person has their photo or initials at the chosen size, which print keeps (D-044). A line deeper
 * than a section continues in a numbered section further down (D-045). Print leaves out the
 * controls.
 */
/** How large the photos next to each name are, on screen and in print (D-044). */
export type BansawaliPhotoSize = 'None' | 'Small' | 'Medium' | 'Large';

@Component({
  selector: 'app-bansawali-page',
  imports: [NgTemplateOutlet, PersonAvatar, RouterLink],
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
  protected readonly photoSizes: readonly { value: BansawaliPhotoSize; label: string }[] = [
    { value: 'None', label: 'None' },
    { value: 'Small', label: 'Small' },
    { value: 'Medium', label: 'Medium' },
    { value: 'Large', label: 'Large' },
  ];
  protected readonly photoSize = signal<BansawaliPhotoSize>('Small');
  protected readonly allChildren = computed(() => this.line() === 'AllChildren');
  protected readonly bansawali = signal<Bansawali | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly tree = computed(() => {
    const bansawali = this.bansawali();
    return bansawali ? bansawaliTree(bansawali) : null;
  });
  protected readonly sectionGenerations = sectionGenerations;
  protected readonly sections = computed(() => {
    const root = this.tree();
    return root ? bansawaliSections(root) : null;
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

  /** The section someone's children continue in, unless it is the section they start. */
  protected continuedIn(entry: BansawaliEntry, section: number): number | null {
    const next = this.sections()?.continuedIn.get(entry.row.person.id);
    return next !== undefined && next !== section ? next : null;
  }

  protected sectionId(section: number): string {
    return `bansawali-section-${section}`;
  }

  protected showSection(section: number): void {
    document.getElementById(this.sectionId(section))?.scrollIntoView();
  }

  protected setPhotoSize(size: BansawaliPhotoSize): void {
    this.photoSize.set(size);
  }

  protected print(): void {
    window.print();
  }
}
