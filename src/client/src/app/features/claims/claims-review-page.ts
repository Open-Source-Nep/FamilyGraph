import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { localNumber } from '../../core/i18n/local-format';
import { LocalDateTimePipe } from '../../core/i18n/local-format.pipes';
import { fullName } from '../people/person';
import { problemMessage } from '../people/problem';
import { Claim, maxEvidenceLength } from './claim';
import { ClaimsService } from './claims.service';

/** What an administrator is writing for one claim. */
interface Draft {
  rationale: string;
  question: string;
}

/**
 * The administrators' Claims page (PRD 04 manual review, D-052): the claims waiting for an
 * administrator, grouped by why (an appeal, no answer in time, competing claims, an already
 * claimed profile), with the claimant's note and the claimed person's reply. An administrator asks
 * the claimant a question, or approves or rejects with a reason. Approving one claim rejects the
 * profile's other claims with the same reason. Relationships and their approvals never change.
 */
@Component({
  selector: 'app-claims-review-page',
  imports: [FormsModule, LocalDateTimePipe, RouterLink],
  templateUrl: './claims-review-page.html',
})
export class ClaimsReviewPage {
  private readonly claims = inject(ClaimsService);

  protected readonly fullName = fullName;
  protected readonly maxLength = maxEvidenceLength;
  protected readonly maxLengthText = localNumber(maxEvidenceLength);
  protected readonly list = signal<Claim[]>([]);
  protected readonly isTruncated = signal(false);
  protected readonly loading = signal(true);
  protected readonly drafts = signal<Record<string, Draft>>({});
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly message = signal<string | null>(null);

  constructor() {
    void this.load();
  }

  protected reason(claim: Claim): string {
    if (claim.appealed) {
      return $localize`:@@claimsReview.reasonAppeal:The claimant appealed the maintainer's rejection.`;
    }
    if (claim.profileClaimed) {
      return $localize`:@@claimsReview.reasonClaimed:The profile is already claimed by someone else.`;
    }
    if ((claim.openClaimsOnProfile ?? 0) > 1) {
      return $localize`:@@claimsReview.reasonCompeting:${localNumber(claim.openClaimsOnProfile ?? 0)}:count: people claim this profile. Approving one rejects the others.`;
    }
    return $localize`:@@claimsReview.reasonUnanswered:The maintainer did not answer in time.`;
  }

  protected draft(claim: Claim): Draft {
    return this.drafts()[claim.id] ?? { rationale: '', question: '' };
  }

  protected setDraft(claim: Claim, change: Partial<Draft>): void {
    this.drafts.update((drafts) => ({
      ...drafts,
      [claim.id]: { ...this.draft(claim), ...change },
    }));
  }

  protected async ask(claim: Claim): Promise<void> {
    await this.run(async () => {
      await this.claims.askForEvidence(claim.id, this.draft(claim).question.trim());
      this.setDraft(claim, { question: '' });
      this.message.set($localize`:@@claimsReview.asked:Your question was sent to the claimant.`);
    });
  }

  protected async decide(claim: Claim, approve: boolean): Promise<void> {
    await this.run(async () => {
      const rationale = this.draft(claim).rationale.trim();
      if (approve) {
        await this.claims.approveAsAdministrator(claim.id, rationale);
        this.message.set(
          $localize`:@@claimsReview.approved:You approved the claim on ${fullName(claim.person)}:name:. The claimant now maintains that profile.`,
        );
      } else {
        await this.claims.rejectAsAdministrator(claim.id, rationale);
        this.message.set($localize`:@@claimsReview.rejected:You rejected the claim.`);
      }
    });
  }

  private async load(): Promise<void> {
    try {
      const list = await this.claims.forAdministrators();
      this.list.set(list.claims);
      this.isTruncated.set(list.isTruncated);
    } catch (error) {
      this.error.set(problemMessage(error));
    } finally {
      this.loading.set(false);
    }
  }

  private async run(action: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    this.message.set(null);
    try {
      await action();
      await this.load();
    } catch (error) {
      this.error.set(problemMessage(error));
    } finally {
      this.busy.set(false);
    }
  }
}
