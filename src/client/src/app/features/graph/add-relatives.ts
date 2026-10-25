import {
  Component,
  ElementRef,
  afterNextRender,
  computed,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';

import { fullName } from '../people/person';
import { problemMessage } from '../people/problem';
import { MarriagesService } from '../relationships/marriages.service';
import { Candidate, PersonPicker } from '../relationships/person-picker';
import { ParentChildType } from '../relationships/relationship';
import { RelationshipsService } from '../relationships/relationships.service';
import {
  SpouseLinkChoices,
  SpouseLinkOutcome,
  createSpouseLinks,
} from '../relationships/spouse-link-choices';
import { GraphPerson } from './graph';
import { AddRelation } from './graph-diagram';

/** What happened to one of the people you added together, or to an extra link to a spouse. */
type AddOutcome = SpouseLinkOutcome;

/** How the dialog ended: whether anything was saved, and what to tell you about it. */
export interface AddRelativesResult {
  changed: boolean;
  message: string | null;
}

/**
 * Adds parents, children or a spouse to a person from the graph page's diagram (D-032). The list
 * starts with your connections: your own profiles and the people you can see by name who are
 * linked to them. Typing also searches every Public profile, so separate families can be joined.
 * On a profile someone else maintains, you can only pick your own profiles, because you must
 * maintain one of the two people (PRD 03). The shared picker's "Add a new person" creates a profile
 * you maintain and selects it (#66, D-038). Parents and children can also be linked to spouses
 * (D-036). The server checks consent, cycles and duplicates; a side maintained by someone else
 * waits for their approval.
 */
@Component({
  selector: 'app-add-relatives',
  imports: [PersonPicker, SpouseLinkChoices],
  templateUrl: './add-relatives.html',
})
export class AddRelatives {
  private readonly relationships = inject(RelationshipsService);
  private readonly marriages = inject(MarriagesService);

  readonly person = input.required<GraphPerson>();
  readonly relation = input.required<AddRelation>();
  readonly closed = output<AddRelativesResult>();

  protected readonly fullName = fullName;
  protected readonly name = computed(() => fullName(this.person()));
  protected readonly title = computed(() => {
    const name = this.name();
    return {
      parent: `Add parents of ${name}`,
      child: `Add children of ${name}`,
      spouse: `Add a spouse of ${name}`,
    }[this.relation()];
  });
  /** The people to add, in the order they were picked, kept while the list is filtered. */
  protected readonly selected = signal<Candidate[]>([]);
  protected readonly type = signal<ParentChildType>('Biological');
  protected readonly selectionMissing = signal(false);
  protected readonly outcomes = signal<AddOutcome[]>([]);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  /** Only when the person is yours can the other one be anybody's (PRD 03). */
  protected readonly searchesEveryone = computed(() => this.person().maintainedByYou);
  protected readonly single = computed(() => this.relation() === 'spouse');
  protected readonly target = computed(() => ({
    id: this.person().id,
    name: this.name(),
    maintainedByYou: this.person().maintainedByYou,
  }));
  protected readonly hint = computed(() =>
    this.searchesEveryone()
      ? 'Your profiles and the people linked to them are listed first. Type a name to search everyone with a public profile.'
      : `You do not maintain ${this.name()}, so you can only add your own profiles. The links stay inactive until its maintainer approves them.`,
  );

  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private readonly spouseChoices = viewChild(SpouseLinkChoices);
  private changed = false;
  private finished = false;

  constructor() {
    afterNextRender(() => {
      const dialog = this.dialog().nativeElement;
      // jsdom has no showModal, so fall back to a non-modal dialog there.
      if (typeof dialog.showModal === 'function') {
        dialog.showModal();
      } else {
        dialog.setAttribute('open', '');
      }
    });
  }

  protected selectionChanged(selected: Candidate[]): void {
    if (selected.length > 0) {
      this.selectionMissing.set(false);
    }
  }

  protected selectedNames(): string {
    return this.selected()
      .map((candidate) => candidate.name)
      .join(', ');
  }

  /**
   * Links each selected person on their own: each is checked, consented to and audited apart, and
   * the people whose link failed stay selected to retry. Then the ticked spouses are linked to the
   * people whose own link was created, with the same type (D-036).
   */
  protected async add(): Promise<void> {
    const picked = this.selected();
    if (picked.length === 0) {
      this.selectionMissing.set(true);
      return;
    }
    const person = this.person();
    const relation = this.relation();
    const type = this.type();
    this.saving.set(true);
    this.error.set(null);
    this.outcomes.set([]);
    const outcomes: AddOutcome[] = [];
    const failed: Candidate[] = [];
    const linked: Candidate[] = [];
    for (const other of picked) {
      try {
        const created =
          relation === 'spouse'
            ? await this.marriages.create({ spouseId: person.id, otherSpouseId: other.id })
            : await this.relationships.create(
                relation === 'parent'
                  ? { parentId: other.id, childId: person.id, type }
                  : { parentId: person.id, childId: other.id, type },
              );
        this.changed = true;
        linked.push(other);
        outcomes.push({ name: other.name, state: created.isActive ? 'active' : 'pending' });
      } catch (error) {
        failed.push(other);
        outcomes.push({ name: other.name, state: 'failed', reason: problemMessage(error) });
      }
    }
    const spouseLinks = this.spouseChoices()?.links(person.id, linked) ?? [];
    const spouseOutcomes = await createSpouseLinks(this.relationships, spouseLinks, type);
    this.changed ||= spouseOutcomes.some((outcome) => outcome.state !== 'failed');
    this.saving.set(false);
    const spouseFailed = spouseOutcomes.some((outcome) => outcome.state === 'failed');
    if (failed.length === 0 && !spouseFailed) {
      this.close(this.summary(outcomes, spouseOutcomes));
      return;
    }
    if (failed.length === 0) {
      // Only a spouse's link failed; the people are linked, so nothing is left to retry.
      this.selected.set([]);
      this.outcomes.set([...outcomes, ...spouseOutcomes]);
      return;
    }
    this.selected.set(failed);
    outcomes.push(...spouseOutcomes);
    if (outcomes.length === 1 && outcomes[0].state === 'failed') {
      this.error.set(outcomes[0].reason);
    } else {
      this.outcomes.set(outcomes);
    }
  }

  /** Closes the dialog. Escape closes it too, which ends here through its `close` event. */
  protected close(message: string | null = null): void {
    if (this.finished) {
      return;
    }
    this.finished = true;
    const dialog = this.dialog().nativeElement;
    if (dialog.open) {
      if (typeof dialog.close === 'function') {
        dialog.close();
      } else {
        dialog.removeAttribute('open');
      }
    }
    this.closed.emit({ changed: this.changed, message });
  }

  private summary(outcomes: AddOutcome[], spouseOutcomes: SpouseLinkOutcome[]): string {
    const all = [...outcomes, ...spouseOutcomes];
    const pending = all.filter((outcome) => outcome.state === 'pending').length;
    const active = outcomes.filter((outcome) => outcome.state === 'active').length;
    const spouseLinks = spouseOutcomes.filter((outcome) => outcome.state !== 'existing').length;
    const parts = [];
    if (active > 0) {
      parts.push(`Linked ${active} ${active === 1 ? 'person' : 'people'} to ${this.name()}.`);
    }
    if (spouseLinks > 0) {
      parts.push(
        spouseLinks === 1
          ? 'Also added one link to a spouse.'
          : `Also added ${spouseLinks} links to spouses.`,
      );
    }
    if (pending > 0) {
      parts.push(
        pending === 1
          ? 'One link is saved but not active until the other maintainer approves it.'
          : `${pending} links are saved but not active until the other maintainers approve them.`,
      );
    }
    return parts.join(' ');
  }
}
