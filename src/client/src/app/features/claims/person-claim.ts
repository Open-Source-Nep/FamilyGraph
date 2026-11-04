import { Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { LocalDateTimePipe } from '../../core/i18n/local-format.pipes';
import { localNumber } from '../../core/i18n/local-format';
import { Person } from '../people/person';
import { problemMessage } from '../people/problem';
import { Claim, ProfileClaimStatus, maxEvidenceLength } from './claim';
import { ClaimDecision } from './claim-decision';
import { ClaimsService } from './claims.service';

/**
 * Identity claims on the person page (PRD 04, D-051). Someone who is this person claims the
 * profile with an optional note, and its maintainer approves or rejects; the maintainer may
 * instead say "This is me". Claims never change the profile's relationships or approvals.
 */
@Component({
  selector: 'app-person-claim',
  imports: [ClaimDecision, FormsModule, LocalDateTimePipe],
  templateUrl: './person-claim.html',
})
export class PersonClaim {
  private readonly claims = inject(ClaimsService);

  readonly person = input.required<Person>();
  /** The profile changed hands: the claimant became its maintainer. */
  readonly changed = output<void>();

  protected readonly maxEvidenceLength = maxEvidenceLength;
  protected readonly maxEvidenceText = localNumber(maxEvidenceLength);
  protected readonly status = signal<ProfileClaimStatus | null>(null);
  protected readonly writing = signal(false);
  protected readonly evidence = signal('');
  /** Whether you are changing the note of your open claim. */
  protected readonly editing = signal(false);
  /** Your replies being written, by claim ID, as the claimed person. */
  protected readonly replies = signal<Record<string, string>>({});
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);

  /** Whether anything about claims applies to you here. */
  protected readonly shown = computed(() => {
    const status = this.status();
    const person = this.person();
    if (!status || person.placeholder === 'Deleted') {
      return false;
    }
    return (
      status.claimedByYou ||
      status.yourClaim !== null ||
      status.toDecide.length > 0 ||
      status.toRespond.length > 0 ||
      status.needsAdministrator ||
      status.claimAgainFrom !== null ||
      this.canClaim() ||
      this.canClaimAsSelf()
    );
  });

  protected readonly canClaimAsSelf = computed(() => {
    const status = this.status();
    return (
      this.person().maintainedByYou &&
      status !== null &&
      status.claimed === false &&
      !status.youHaveClaim &&
      status.toDecide.length === 0 &&
      !status.needsAdministrator
    );
  });

  protected readonly canClaim = computed(() => {
    const status = this.status();
    return (
      !this.person().maintainedByYou &&
      status !== null &&
      !status.youHaveClaim &&
      !status.claimedByYou &&
      status.claimAgainFrom === null
    );
  });

  constructor() {
    effect(() => {
      const personId = this.person().id;
      this.status.set(null);
      this.writing.set(false);
      this.editing.set(false);
      this.replies.set({});
      this.error.set(null);
      this.notice.set(null);
      void this.load(personId);
    });
  }

  protected async submit(): Promise<void> {
    await this.run(async () => {
      const note = this.evidence().trim();
      await this.claims.submit(this.person().id, note === '' ? null : note);
      this.writing.set(false);
      this.evidence.set('');
      this.notice.set(
        $localize`:@@claims.submitted:Your claim was sent to the maintainer of this profile.`,
      );
    });
  }

  protected async withdraw(claim: Claim): Promise<void> {
    await this.run(async () => {
      await this.claims.withdraw(claim.id);
      this.notice.set($localize`:@@claims.withdrawn:You withdrew your claim.`);
    });
  }

  protected startEditing(claim: Claim): void {
    this.evidence.set(claim.evidence ?? '');
    this.editing.set(true);
  }

  protected async saveEvidence(claim: Claim): Promise<void> {
    await this.run(async () => {
      const note = this.evidence().trim();
      await this.claims.changeEvidence(claim.id, note === '' ? null : note);
      this.editing.set(false);
      this.evidence.set('');
      this.notice.set($localize`:@@claims.noteSaved:Your note was saved.`);
    });
  }

  protected async requestReview(claim: Claim): Promise<void> {
    await this.run(async () => {
      await this.claims.requestReview(claim.id);
      this.notice.set(
        $localize`:@@claims.reviewRequested:Your claim was sent to the administrators. They may ask you for more information here.`,
      );
    });
  }

  protected reply(claim: Claim): string {
    return this.replies()[claim.id] ?? claim.maintainerResponse ?? '';
  }

  protected setReply(claim: Claim, text: string): void {
    this.replies.update((replies) => ({ ...replies, [claim.id]: text }));
  }

  protected async respond(claim: Claim): Promise<void> {
    await this.run(async () => {
      const text = this.reply(claim).trim();
      await this.claims.respond(claim.id, text === '' ? null : text);
      this.notice.set($localize`:@@claims.replySaved:Your reply was saved for the administrators.`);
    });
  }

  protected async claimAsSelf(): Promise<void> {
    await this.run(async () => {
      await this.claims.claimAsSelf(this.person().id);
      this.notice.set(null);
    });
  }

  protected async decide(claim: Claim, approve: boolean): Promise<void> {
    await this.run(async () => {
      if (approve) {
        await this.claims.approve(claim.id);
        this.notice.set(
          $localize`:@@claims.approved:You approved the claim. The claimant now maintains this profile.`,
        );
        this.changed.emit();
      } else {
        await this.claims.reject(claim.id);
        this.notice.set($localize`:@@claims.rejected:You rejected the claim.`);
      }
    });
  }

  private async load(personId: string): Promise<void> {
    try {
      const status = await this.claims.forPerson(personId);
      if (this.person().id === personId) {
        this.status.set(status);
      }
    } catch (error) {
      this.error.set(problemMessage(error));
    }
  }

  private async run(action: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      await action();
      await this.load(this.person().id);
    } catch (error) {
      this.error.set(problemMessage(error));
    } finally {
      this.busy.set(false);
    }
  }
}
