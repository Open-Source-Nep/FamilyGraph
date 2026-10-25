import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { Person, fullName } from '../people/person';
import { problemMessage } from '../people/problem';
import { MarriagesService } from './marriages.service';
import { Candidate, PersonPicker } from './person-picker';
import { Spouse, SpouseList, relationHint } from './relationship';

/** How you can remove a marriage: withdraw your side's approval, or delete it when you maintain both. */
type Removal = { kind: 'withdraw'; spouseId: string } | { kind: 'delete' };

/**
 * A person's spouses through active marriages, current ones first (D-031), and a form to pick their
 * spouse with the shared picker, which can also add a new person (D-038). When you maintain the
 * person the spouse can be anyone you can see; otherwise only one of your own profiles (PRD 03). Where you maintain either spouse you can mark the marriage former
 * or current again; where you maintain one you can withdraw your approval, and where you maintain
 * both you can delete it (PRD 03).
 */
@Component({
  selector: 'app-person-spouses',
  imports: [PersonPicker, RouterLink],
  templateUrl: './person-spouses.html',
})
export class PersonSpouses {
  private readonly marriages = inject(MarriagesService);

  readonly person = input.required<Person>();

  protected readonly fullName = fullName;
  protected readonly relationHint = relationHint;
  protected readonly spouses = signal<SpouseList | null>(null);
  protected readonly adding = signal(false);
  /** The spouse to add, at most one. */
  protected readonly selected = signal<Candidate[]>([]);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);
  /** The marriage whose withdrawal or deletion waits for confirmation. */
  protected readonly confirming = signal<string | null>(null);
  protected readonly name = computed(() => fullName(this.person()));
  protected readonly personId = computed(() => this.person().id);
  protected readonly hint = computed(() =>
    this.person().maintainedByYou
      ? 'Your profiles and the people linked to them are listed first. Type a name to search everyone with a public profile.'
      : `You do not maintain ${this.name()}, so you can only pick your own profiles. The marriage stays inactive until its maintainer approves it.`,
  );

  constructor() {
    effect(() => {
      const id = this.personId();
      this.spouses.set(null);
      this.adding.set(false);
      this.error.set(null);
      this.notice.set(null);
      this.confirming.set(null);
      this.load(id);
    });
  }

  /** Either spouse's maintainer may mark the marriage former or current, unless their profile is deleted. */
  protected canMark(spouse: Spouse): boolean {
    const person = this.person();
    return (
      (person.maintainedByYou && person.placeholder !== 'Deleted') ||
      (spouse.maintainedByYou && spouse.placeholder !== 'Deleted')
    );
  }

  protected removal(spouse: Spouse): Removal | null {
    const maintainsPerson = this.person().maintainedByYou;
    if (maintainsPerson && spouse.maintainedByYou) {
      return { kind: 'delete' };
    }
    if (!maintainsPerson && !spouse.maintainedByYou) {
      return null;
    }
    return { kind: 'withdraw', spouseId: maintainsPerson ? this.personId() : spouse.id };
  }

  protected async setFormer(spouse: Spouse, isFormer: boolean): Promise<void> {
    await this.act(async () => {
      await this.marriages.setFormer(spouse.marriageId, isFormer);
      this.notice.set(
        isFormer
          ? `The marriage to ${fullName(spouse)} is now shown as former.`
          : `The marriage to ${fullName(spouse)} is now shown as current.`,
      );
    });
  }

  protected async remove(spouse: Spouse, removal: Removal): Promise<void> {
    await this.act(async () => {
      if (removal.kind === 'delete') {
        await this.marriages.delete(spouse.marriageId);
        this.notice.set(`You deleted the marriage to ${fullName(spouse)}.`);
      } else {
        await this.marriages.disagree(spouse.marriageId, removal.spouseId);
        this.notice.set(
          `You withdrew your approval, so the marriage to ${fullName(spouse)} is no longer active. You can restore it on the Approvals page.`,
        );
      }
      this.confirming.set(null);
    });
  }

  protected startAdding(): void {
    this.adding.set(true);
    this.notice.set(null);
    this.error.set(null);
    this.selected.set([]);
  }

  protected async add(): Promise<void> {
    const [spouse] = this.selected();
    if (!spouse) {
      this.error.set('Choose the spouse.');
      return;
    }
    await this.act(async () => {
      const marriage = await this.marriages.create({
        spouseId: spouse.id,
        otherSpouseId: this.personId(),
      });
      this.adding.set(false);
      if (!marriage.isActive) {
        this.notice.set(
          'The marriage is saved but not active yet. It becomes active when the maintainer of the other profile approves it.',
        );
      }
    });
  }

  private async act(change: () => Promise<void>): Promise<void> {
    const id = this.personId();
    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);
    try {
      await change();
      this.load(id);
    } catch (error) {
      this.error.set(problemMessage(error));
    } finally {
      this.saving.set(false);
    }
  }

  private load(id: string): void {
    this.marriages.listSpouses(id).then(
      (spouses) => {
        if (this.personId() === id) {
          this.spouses.set(spouses);
        }
      },
      (error: unknown) => this.error.set(problemMessage(error)),
    );
  }
}
