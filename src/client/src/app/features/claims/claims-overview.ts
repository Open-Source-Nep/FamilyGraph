import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { LocalDateTimePipe } from '../../core/i18n/local-format.pipes';
import { fullName } from '../people/person';
import { problemMessage } from '../people/problem';
import { Claim, ClaimList, ClaimState } from './claim';
import { ClaimDecision } from './claim-decision';
import { ClaimsService } from './claims.service';

/**
 * The claims on the Approvals page (D-051): claims on your profiles that you decide, and the
 * claims you sent with where they stand. Nothing shows when there are none.
 */
@Component({
  selector: 'app-claims-overview',
  imports: [ClaimDecision, LocalDateTimePipe, RouterLink],
  templateUrl: './claims-overview.html',
})
export class ClaimsOverview {
  private readonly claims = inject(ClaimsService);

  protected readonly fullName = fullName;
  protected readonly toDecide = signal<ClaimList | null>(null);
  protected readonly mine = signal<ClaimList | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);

  constructor() {
    void this.load();
  }

  protected stateLabel(state: ClaimState): string {
    switch (state) {
      case 'Open':
        return $localize`:@@claims.stateOpen:Waiting for the maintainer`;
      case 'Approved':
        return $localize`:@@claims.stateApproved:Approved: the profile is you`;
      case 'Rejected':
        return $localize`:@@claims.stateRejected:Rejected`;
      case 'Withdrawn':
        return $localize`:@@claims.stateWithdrawn:Withdrawn`;
      case 'Cancelled':
        return $localize`:@@claims.stateCancelled:Cancelled: the profile was deleted`;
    }
  }

  protected async decide(claim: Claim, approve: boolean): Promise<void> {
    await this.run(async () => {
      if (approve) {
        await this.claims.approve(claim.id);
        this.notice.set(
          $localize`:@@claims.approvedNamed:You approved the claim on ${fullName(claim.person)}:name:. The claimant now maintains that profile.`,
        );
      } else {
        await this.claims.reject(claim.id);
        this.notice.set($localize`:@@claims.rejected:You rejected the claim.`);
      }
    });
  }

  protected async withdraw(claim: Claim): Promise<void> {
    await this.run(async () => {
      await this.claims.withdraw(claim.id);
      this.notice.set($localize`:@@claims.withdrawn:You withdrew your claim.`);
    });
  }

  private async load(): Promise<void> {
    try {
      const [toDecide, mine] = await Promise.all([this.claims.toDecide(), this.claims.mine()]);
      this.toDecide.set(toDecide);
      this.mine.set(mine);
    } catch (error) {
      this.error.set(problemMessage(error));
    }
  }

  private async run(action: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    this.notice.set(null);
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
