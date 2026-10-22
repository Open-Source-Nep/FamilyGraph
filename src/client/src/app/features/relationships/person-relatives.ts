import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { PeopleService } from '../people/people.service';
import { Person, PersonSummary, fullName } from '../people/person';
import { problemMessage } from '../people/problem';
import { DirectRelatives, LinkEndpoint, ParentChildType, Relative } from './relationship';
import { RelationshipsService } from './relationships.service';

type Side = 'parent' | 'child';

/**
 * A person's parents and children through active links, and a form to link one of your own
 * profiles as a parent or child (PRD 02). Adoptive links are labelled as such (PRD 10). Where you
 * maintain one of the two people, you can withdraw your approval of the link (PRD 03).
 */
@Component({
  selector: 'app-person-relatives',
  imports: [ReactiveFormsModule, RouterLink],
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
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);
  protected readonly name = computed(() => fullName(this.person()));
  /** The link whose withdrawal waits for confirmation. */
  protected readonly confirming = signal<string | null>(null);
  /** Renaming the person gives a new object but must not reload the lists. */
  private readonly personId = computed(() => this.person().id);

  protected readonly form = new FormGroup({
    side: new FormControl<Side>('parent', { nonNullable: true }),
    otherId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
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
      this.load(id);
    });
  }

  /**
   * Your side of the link to a relative, when you maintain exactly one of the two people. Someone
   * who maintains both deletes the link instead (D-013, D-014).
   */
  protected withdrawableSide(relative: Relative, relativeIs: Side): LinkEndpoint | null {
    const maintainsPerson = this.person().maintainedByYou;
    if (maintainsPerson === relative.maintainedByYou) {
      return null;
    }
    const relativeSide: LinkEndpoint = relativeIs === 'parent' ? 'Parent' : 'Child';
    const personSide: LinkEndpoint = relativeIs === 'parent' ? 'Child' : 'Parent';
    return maintainsPerson ? personSide : relativeSide;
  }

  protected async withdraw(relative: Relative, endpoint: LinkEndpoint): Promise<void> {
    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);
    try {
      await this.relationships.disagree(relative.linkId, endpoint);
      this.confirming.set(null);
      this.notice.set(
        `You withdrew your approval, so the link to ${fullName(relative)} is no longer active. You can restore it on the Approvals page.`,
      );
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
    try {
      const list = await this.people.listMaintained();
      this.candidates.set(list.people.filter((person) => person.id !== this.personId()));
      this.candidatesTruncated.set(list.isTruncated);
    } catch (error) {
      this.error.set(problemMessage(error));
    }
  }

  protected async add(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const { side, otherId, type } = this.form.getRawValue();
    const id = this.personId();
    this.saving.set(true);
    this.error.set(null);
    try {
      const link = await this.relationships.create(
        side === 'parent'
          ? { parentId: otherId, childId: id, type }
          : { parentId: id, childId: otherId, type },
      );
      this.adding.set(false);
      if (link.isActive) {
        this.load(id);
      } else {
        this.notice.set(
          'The link is saved but not active yet. It becomes active when the maintainer of the other profile approves it.',
        );
      }
    } catch (error) {
      this.error.set(problemMessage(error));
    } finally {
      this.saving.set(false);
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
