import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';

import { fullName } from '../people/person';
import { problemMessage } from '../people/problem';
import { MarriagesService } from './marriages.service';
import { ParentChildType, Spouse, relationWord } from './relationship';
import { RelationshipsService } from './relationships.service';

/** Someone picked as a parent or child, by the name you see. */
export interface PickedPerson {
  id: string;
  name: string;
  maintainedByYou: boolean;
}

/** The person the parents or children are added to. A profile not created yet has no ID and no spouses. */
export interface LinkTarget {
  id: string | null;
  name: string;
  maintainedByYou: boolean;
}

/** One extra link to a spouse, with the wording for its outcome. */
export interface SpouseLink {
  parentId: string;
  childId: string;
  label: string;
}

/**
 * What happened to one link created with others: `existing` when the two were already linked, which
 * is what the box asked for.
 */
export type SpouseLinkOutcome =
  | { name: string; state: 'active' | 'pending' | 'existing' }
  | { name: string; state: 'failed'; reason: string };

/** Creates each extra link on its own, with the type you picked for the others. */
export async function createSpouseLinks(
  relationships: RelationshipsService,
  links: readonly SpouseLink[],
  type: ParentChildType,
): Promise<SpouseLinkOutcome[]> {
  const outcomes: SpouseLinkOutcome[] = [];
  for (const { parentId, childId, label } of links) {
    try {
      const created = await relationships.create({ parentId, childId, type });
      outcomes.push({ name: label, state: created.isActive ? 'active' : 'pending' });
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.error?.code === 'link.duplicate') {
        outcomes.push({ name: label, state: 'existing' });
      } else {
        outcomes.push({ name: label, state: 'failed', reason: problemMessage(error) });
      }
    }
  }
  return outcomes;
}

/** A spouse you can also link, and whose spouse they are. */
interface Choice {
  spouse: Spouse;
  of: { id: string; name: string };
  checked: boolean;
}

/**
 * Offers to link spouses too (#88, D-036). Adding children to A offers each of A's spouses as their
 * other parent; adding a parent offers each of that parent's spouses as a parent too. Current
 * spouses start ticked and former ones unticked. Only the spouses through active marriages are
 * listed (D-031), never a deleted one (no new links), and only where you maintain one side of the
 * link (PRD 03). Each extra link is an ordinary link with its own consent.
 */
@Component({
  selector: 'app-spouse-link-choices',
  templateUrl: './spouse-link-choices.html',
})
export class SpouseLinkChoices {
  private readonly marriages = inject(MarriagesService);

  /** What the picked people become: the person's parents, or their children. */
  readonly relation = input.required<'parent' | 'child'>();
  readonly person = input.required<LinkTarget>();
  readonly picked = input.required<readonly PickedPerson[]>();

  protected readonly error = signal<string | null>(null);
  /** Spouses by the person they are married to, once loaded. */
  private readonly spouses = signal<ReadonlyMap<string, readonly Spouse[]>>(new Map());
  /** The boxes you changed, by spouse ID; the others keep their default. */
  private readonly ticked = signal<ReadonlyMap<string, boolean>>(new Map());
  private readonly loading = new Set<string>();

  protected readonly choices = computed<Choice[]>(() => {
    const person = this.person();
    const picked = this.picked();
    if (picked.length === 0) {
      return [];
    }
    const pickedIds = new Set(picked.map((other) => other.id));
    const owners =
      this.relation() === 'child'
        ? person.id === null
          ? []
          : [{ id: person.id, name: person.name }]
        : picked.map((other) => ({ id: other.id, name: other.name }));
    const choices: Choice[] = [];
    const seen = new Set<string>();
    for (const owner of owners) {
      for (const spouse of this.spouses().get(owner.id) ?? []) {
        if (
          spouse.placeholder === 'Deleted' ||
          spouse.id === person.id ||
          pickedIds.has(spouse.id) ||
          seen.has(spouse.id) ||
          !this.mayLink(spouse, person, picked)
        ) {
          continue;
        }
        seen.add(spouse.id);
        choices.push({
          spouse,
          of: owner,
          checked: this.ticked().get(spouse.id) ?? !spouse.isFormer,
        });
      }
    }
    return choices;
  });

  constructor() {
    effect(() => {
      const ids =
        this.relation() === 'child'
          ? [this.person().id].filter((id): id is string => id !== null)
          : this.picked().map((other) => other.id);
      untracked(() => ids.forEach((id) => void this.load(id)));
    });
  }

  protected label({ spouse, of }: Choice): string {
    const word = relationWord(
      'spouse',
      spouse.gender,
      spouse.isFormer ? 'FormerMarriage' : 'Marriage',
    );
    return $localize`:@@spouseLinks.choice:${fullName(spouse)}:spouse:, ${word}:role: of ${of.name}:name:`;
  }

  protected tick(spouseId: string, checked: boolean): void {
    this.ticked.update((map) => new Map(map).set(spouseId, checked));
  }

  /**
   * The extra links for the ticked spouses: each ticked spouse of the person becomes a parent of
   * each of `linked`, or each ticked spouse of a parent in `linked` becomes a parent of the person.
   * `personId` names the person once it exists. Only pass the people whose own link was created.
   */
  links(personId: string, linked: readonly PickedPerson[]): SpouseLink[] {
    const personName = this.person().name;
    const linkedIds = new Set(linked.map((other) => other.id));
    const links: SpouseLink[] = [];
    for (const { spouse, of, checked } of this.choices()) {
      if (!checked) {
        continue;
      }
      const spouseName = fullName(spouse);
      if (this.relation() === 'child') {
        for (const child of linked) {
          links.push({
            parentId: spouse.id,
            childId: child.id,
            label: $localize`:@@spouseLinks.asChildOf:${child.name}:child: as a child of ${spouseName}:parent:`,
          });
        }
      } else if (linkedIds.has(of.id)) {
        links.push({
          parentId: spouse.id,
          childId: personId,
          label: $localize`:@@spouseLinks.asParentOf:${spouseName}:parent: as a parent of ${personName}:child:`,
        });
      }
    }
    return links;
  }

  /** You must maintain the spouse or the other end of at least one of its links (PRD 03). */
  private mayLink(spouse: Spouse, person: LinkTarget, picked: readonly PickedPerson[]): boolean {
    if (spouse.maintainedByYou) {
      return true;
    }
    return this.relation() === 'child'
      ? picked.some((child) => child.maintainedByYou)
      : person.maintainedByYou;
  }

  private async load(id: string): Promise<void> {
    if (this.spouses().has(id) || this.loading.has(id)) {
      return;
    }
    this.loading.add(id);
    try {
      const list = await this.marriages.listSpouses(id);
      this.spouses.update((map) => new Map(map).set(id, list.spouses));
    } catch (error) {
      this.error.set(problemMessage(error));
    } finally {
      this.loading.delete(id);
    }
  }
}
