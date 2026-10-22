import { NgTemplateOutlet } from '@angular/common';
import {
  Component,
  ElementRef,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { PeopleService } from '../people/people.service';
import { Person, PersonName, PersonSummary, fullName } from '../people/person';
import { PersonNameForm } from '../people/person-name-form';
import { problemMessage } from '../people/problem';
import { DirectRelatives, LinkEndpoint, ParentChildType, Relative } from './relationship';
import { RelationshipsService } from './relationships.service';

type Side = 'parent' | 'child';

/** How you can remove a link: withdraw your side's approval, or delete it when you maintain both people. */
type Removal = { kind: 'withdraw'; endpoint: LinkEndpoint } | { kind: 'delete' };

/** What happened to one of the links you submitted together. Each is created on its own. */
type LinkOutcome =
  { name: string; state: 'active' | 'pending' } | { name: string; state: 'failed'; reason: string };

/**
 * A person's parents and children through active links, and a form to link one or more of your own
 * profiles as parents or children (PRD 02), with a dialog to add a new person on the way (#66). Adoptive links are labelled as such (PRD 10). Where you
 * maintain one of the two people, you can withdraw your approval of the link; where you maintain
 * both, you can delete it (PRD 03, D-014).
 */
@Component({
  selector: 'app-person-relatives',
  imports: [NgTemplateOutlet, PersonNameForm, ReactiveFormsModule, RouterLink],
  templateUrl: './person-relatives.html',
})
export class PersonRelatives {
  private readonly relationships = inject(RelationshipsService);
  private readonly people = inject(PeopleService);

  readonly person = input.required<Person>();

  protected readonly fullName = fullName;
  protected readonly relatives = signal<DirectRelatives | null>(null);
  protected readonly adding = signal(false);
  /** The profiles you maintain, other than this person, once the form has loaded them. */
  protected readonly candidates = signal<PersonSummary[] | null>(null);
  protected readonly candidatesTruncated = signal(false);
  /** The ids of the profiles to link, in the order they were ticked. */
  protected readonly selected = signal<string[]>([]);
  protected readonly selectionMissing = signal(false);
  protected readonly outcomes = signal<LinkOutcome[]>([]);
  protected readonly creatingPerson = signal(false);
  protected readonly personError = signal<string | null>(null);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);
  protected readonly name = computed(() => fullName(this.person()));
  /** The link whose withdrawal or deletion waits for confirmation. */
  protected readonly confirming = signal<string | null>(null);
  private readonly newPersonDialog = viewChild<ElementRef<HTMLDialogElement>>('newPersonDialog');
  /** Renaming the person gives a new object but must not reload the lists. */
  private readonly personId = computed(() => this.person().id);

  protected readonly form = new FormGroup({
    side: new FormControl<Side>('parent', { nonNullable: true }),
    type: new FormControl<ParentChildType>('Biological', { nonNullable: true }),
  });

  constructor() {
    effect(() => {
      const id = this.personId();
      this.relatives.set(null);
      this.adding.set(false);
      this.error.set(null);
      this.notice.set(null);
      this.confirming.set(null);
      untracked(() => this.closeNewPerson());
      this.load(id);
    });
  }

  protected isSelected(id: string): boolean {
    return this.selected().includes(id);
  }

  protected toggle(id: string, checked: boolean): void {
    this.selected.update((ids) =>
      checked ? (ids.includes(id) ? ids : [...ids, id]) : ids.filter((other) => other !== id),
    );
    if (checked) {
      this.selectionMissing.set(false);
    }
  }

  protected openNewPerson(): void {
    this.personError.set(null);
    this.creatingPerson.set(true);
    const dialog = this.newPersonDialog()?.nativeElement;
    // jsdom has no showModal, so fall back to a non-modal dialog there.
    if (dialog && !dialog.open) {
      if (typeof dialog.showModal === 'function') {
        dialog.showModal();
      } else {
        dialog.setAttribute('open', '');
      }
    }
  }

  /** Closes the dialog. Escape closes it too, which ends here through its `close` event. */
  protected closeNewPerson(): void {
    this.creatingPerson.set(false);
    const dialog = this.newPersonDialog()?.nativeElement;
    if (dialog?.open) {
      if (typeof dialog.close === 'function') {
        dialog.close();
      } else {
        dialog.removeAttribute('open');
      }
    }
  }

  /** Creates a profile you maintain, then lists and selects it for linking (PRD 01). */
  protected async createPerson(name: PersonName): Promise<void> {
    this.saving.set(true);
    this.personError.set(null);
    try {
      const person = await this.people.create(name);
      this.candidates.update((list) => [...(list ?? []), person]);
      this.toggle(person.id, true);
      this.closeNewPerson();
    } catch (error) {
      this.personError.set(problemMessage(error));
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

  protected async startAdding(): Promise<void> {
    this.adding.set(true);
    this.notice.set(null);
    this.error.set(null);
    this.form.reset();
    this.selected.set([]);
    this.selectionMissing.set(false);
    this.outcomes.set([]);
    try {
      const list = await this.people.listMaintained();
      this.candidates.set(list.people.filter((person) => person.id !== this.personId()));
      this.candidatesTruncated.set(list.isTruncated);
    } catch (error) {
      this.error.set(problemMessage(error));
    }
  }

  /**
   * Creates one link per selected profile. They are not atomic: each is checked, consented to and
   * audited on its own, and the profiles whose link failed stay selected to retry.
   */
  protected async add(): Promise<void> {
    const ids = this.selected();
    if (ids.length === 0) {
      this.selectionMissing.set(true);
      return;
    }
    const { side, type } = this.form.getRawValue();
    const id = this.personId();
    const names = new Map((this.candidates() ?? []).map((person) => [person.id, fullName(person)]));
    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);
    this.outcomes.set([]);
    const outcomes: LinkOutcome[] = [];
    const failedIds: string[] = [];
    for (const otherId of ids) {
      const name = names.get(otherId) ?? '';
      try {
        const link = await this.relationships.create(
          side === 'parent'
            ? { parentId: otherId, childId: id, type }
            : { parentId: id, childId: otherId, type },
        );
        outcomes.push({ name, state: link.isActive ? 'active' : 'pending' });
      } catch (error) {
        failedIds.push(otherId);
        outcomes.push({ name, state: 'failed', reason: problemMessage(error) });
      }
    }
    this.saving.set(false);
    if (this.personId() !== id) {
      return;
    }
    if (outcomes.some((outcome) => outcome.state === 'active')) {
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
    if (failedIds.length === 0) {
      this.adding.set(false);
    } else {
      this.selected.set(failedIds);
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
