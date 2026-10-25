import { NgTemplateOutlet } from '@angular/common';
import { Component, computed, effect, inject, input, signal, viewChild } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { Person, fullName } from '../people/person';
import { problemMessage } from '../people/problem';
import { Candidate, PersonPicker } from './person-picker';
import {
  DirectRelatives,
  LinkEndpoint,
  ParentChildType,
  Relative,
  relationHint,
} from './relationship';
import { RelationshipsService } from './relationships.service';
import {
  PickedPerson,
  SpouseLinkChoices,
  SpouseLinkOutcome,
  createSpouseLinks,
} from './spouse-link-choices';

type Side = 'parent' | 'child';

/** How you can remove a link: withdraw your side's approval, or delete it when you maintain both people. */
type Removal = { kind: 'withdraw'; endpoint: LinkEndpoint } | { kind: 'delete' };

/** What happened to one of the links you submitted together. Each is created on its own. */
type LinkOutcome = SpouseLinkOutcome;

/**
 * A person's parents and children through active links, and a form to link one or more people as
 * parents or children (PRD 02) with the shared picker, which can also add a new person (#66,
 * D-038). When you maintain the person, the others can be anyone you can see; otherwise only your
 * own profiles (PRD 03). Adoptive links are labelled as such (PRD 10). Where you maintain one of
 * the two people, you can withdraw your approval of the link; where you maintain both, you can
 * delete it (PRD 03, D-014).
 */
@Component({
  selector: 'app-person-relatives',
  imports: [NgTemplateOutlet, PersonPicker, ReactiveFormsModule, RouterLink, SpouseLinkChoices],
  templateUrl: './person-relatives.html',
})
export class PersonRelatives {
  private readonly relationships = inject(RelationshipsService);

  readonly person = input.required<Person>();

  protected readonly fullName = fullName;
  protected readonly relationHint = relationHint;
  protected readonly relatives = signal<DirectRelatives | null>(null);
  protected readonly adding = signal(false);
  /** The people to link, in the order they were picked. */
  protected readonly selected = signal<Candidate[]>([]);
  protected readonly selectionMissing = signal(false);
  protected readonly outcomes = signal<LinkOutcome[]>([]);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);
  protected readonly name = computed(() => fullName(this.person()));
  /** The link whose withdrawal or deletion waits for confirmation. */
  protected readonly confirming = signal<string | null>(null);
  private readonly spouseChoices = viewChild(SpouseLinkChoices);
  /** Renaming the person gives a new object but must not reload the lists. */
  protected readonly personId = computed(() => this.person().id);

  protected readonly form = new FormGroup({
    side: new FormControl<Side>('parent', { nonNullable: true }),
    type: new FormControl<ParentChildType>('Biological', { nonNullable: true }),
  });
  protected readonly side = toSignal(this.form.controls.side.valueChanges, {
    initialValue: this.form.controls.side.value,
  });
  protected readonly target = computed(() => ({
    id: this.personId(),
    name: this.name(),
    maintainedByYou: this.person().maintainedByYou,
  }));
  protected readonly hint = computed(() =>
    this.person().maintainedByYou
      ? 'Your profiles and the people linked to them are listed first. Type a name to search everyone with a public profile.'
      : `You do not maintain ${this.name()}, so you can only pick your own profiles. The links stay inactive until its maintainer approves them.`,
  );

  constructor() {
    effect(() => {
      const id = this.personId();
      this.relatives.set(null);
      this.adding.set(false);
      this.error.set(null);
      this.notice.set(null);
      this.confirming.set(null);
      this.load(id);
    });
  }

  protected selectionChanged(selected: Candidate[]): void {
    if (selected.length > 0) {
      this.selectionMissing.set(false);
    }
  }

  /**
   * Moves a child one place up (-1) or down (+1) in this parent's birth order. The whole list as
   * shown is numbered 1, 2, 3…, so unordered children become ordered (D-016, D-037).
   */
  protected async move(index: number, offset: -1 | 1): Promise<void> {
    const relatives = this.relatives();
    const target = index + offset;
    if (!relatives || target < 0 || target >= relatives.children.length) {
      return;
    }
    const children = [...relatives.children];
    [children[index], children[target]] = [children[target], children[index]];
    this.saving.set(true);
    this.error.set(null);
    try {
      await this.relationships.orderChildren(
        this.personId(),
        children.map((child) => child.linkId),
      );
      this.relatives.set({
        ...relatives,
        children: children.map((child, position) => ({ ...child, birthOrder: position + 1 })),
      });
    } catch (error) {
      this.error.set(problemMessage(error));
    } finally {
      this.saving.set(false);
    }
  }

  /**
   * How you can remove the link to a relative: withdraw your side when you maintain exactly one of
   * the two people, delete the link when you maintain both (D-013, D-014), or nothing.
   */
  protected removal(relative: Relative, relativeIs: Side): Removal | null {
    const maintainsPerson = this.person().maintainedByYou;
    if (maintainsPerson && relative.maintainedByYou) {
      return { kind: 'delete' };
    }
    if (!maintainsPerson && !relative.maintainedByYou) {
      return null;
    }
    const relativeSide: LinkEndpoint = relativeIs === 'parent' ? 'Parent' : 'Child';
    const personSide: LinkEndpoint = relativeIs === 'parent' ? 'Child' : 'Parent';
    return { kind: 'withdraw', endpoint: maintainsPerson ? personSide : relativeSide };
  }

  protected async remove(relative: Relative, removal: Removal): Promise<void> {
    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);
    try {
      if (removal.kind === 'delete') {
        await this.relationships.delete(relative.linkId);
        this.notice.set(`You deleted the link to ${fullName(relative)}.`);
      } else {
        await this.relationships.disagree(relative.linkId, removal.endpoint);
        this.notice.set(
          `You withdrew your approval, so the link to ${fullName(relative)} is no longer active. You can restore it on the Approvals page.`,
        );
      }
      this.confirming.set(null);
      this.load(this.personId());
    } catch (error) {
      this.error.set(problemMessage(error));
    } finally {
      this.saving.set(false);
    }
  }

  protected startAdding(): void {
    this.adding.set(true);
    this.notice.set(null);
    this.error.set(null);
    this.form.reset();
    this.selected.set([]);
    this.selectionMissing.set(false);
    this.outcomes.set([]);
  }

  /**
   * Creates one link per selected person. They are not atomic: each is checked, consented to and
   * audited on its own, and the people whose link failed stay selected to retry. Then the ticked
   * spouses are linked to the profiles whose own link was created, with the same type (D-036).
   */
  protected async add(): Promise<void> {
    const picked = this.selected();
    if (picked.length === 0) {
      this.selectionMissing.set(true);
      return;
    }
    const { side, type } = this.form.getRawValue();
    const id = this.personId();
    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);
    this.outcomes.set([]);
    const outcomes: LinkOutcome[] = [];
    const failed: Candidate[] = [];
    const linked: PickedPerson[] = [];
    for (const other of picked) {
      try {
        const link = await this.relationships.create(
          side === 'parent'
            ? { parentId: other.id, childId: id, type }
            : { parentId: id, childId: other.id, type },
        );
        linked.push(other);
        outcomes.push({ name: other.name, state: link.isActive ? 'active' : 'pending' });
      } catch (error) {
        failed.push(other);
        outcomes.push({ name: other.name, state: 'failed', reason: problemMessage(error) });
      }
    }
    const spouseLinks = this.spouseChoices()?.links(id, linked) ?? [];
    outcomes.push(...(await createSpouseLinks(this.relationships, spouseLinks, type)));
    this.saving.set(false);
    if (this.personId() !== id) {
      return;
    }
    if (outcomes.some((outcome) => outcome.state === 'active' || outcome.state === 'existing')) {
      this.load(id);
    }
    if (outcomes.length === 1) {
      // A single link keeps the plain messages.
      const [outcome] = outcomes;
      if (outcome.state === 'failed') {
        this.error.set(outcome.reason);
        return;
      }
      if (outcome.state === 'pending') {
        this.notice.set(
          'The link is saved but not active yet. It becomes active when the maintainer of the other profile approves it.',
        );
      }
    } else {
      this.outcomes.set(outcomes);
    }
    if (failed.length === 0) {
      this.adding.set(false);
    } else {
      this.selected.set(failed);
    }
  }

  private load(id: string): void {
    this.relationships.listDirectRelatives(id).then(
      (relatives) => {
        if (this.personId() === id) {
          this.relatives.set(relatives);
        }
      },
      (error: unknown) => this.error.set(problemMessage(error)),
    );
  }
}
