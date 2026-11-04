import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { LocalDateTimePipe } from '../../core/i18n/local-format.pipes';
import { fullName } from '../people/person';
import { problemMessage } from '../people/problem';
import { Claim, ClaimList } from './claim';
import { ClaimDecision } from './claim-decision';
import { ClaimsService } from './claims.service';

/**
 * The claims on the Approvals page (D-051, D-052): claims on your profiles that you decide, claims
 * on your claimed profile that you may reply to, and the claims you sent with where they stand.
 * Nothing shows when there are none.
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
  protected readonly toRespond = signal<ClaimList | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);

  constructor() {
    void this.load();
  }

  protected stateLabel(claim: Claim): string {
    switch (claim.state) {
      case 'Open':
        return claim.withAdministrators
          ? $localize`:@@claims.stateWithAdministrators:Waiting for an administrator`
          : $localize`:@@claims.stateOpen:Waiting for the maintainer`;
      case 'Approved':
        return $localize`:@@claims.stateApproved:Approved: the profile is you`;
      case 'Rejected':
        return claim.decidedByAdministrator
          ? $localize`:@@claims.stateRejectedByAdministrator:Rejected by an administrator`
          : $localize`:@@claims.stateRejected:Rejected`;
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

  protected async requestReview(claim: Claim): Promise<void> {
    await this.run(async () => {
      await this.claims.requestReview(claim.id);
      this.notice.set(
        $localize`:@@claims.reviewRequested:Your claim was sent to the administrators. They may ask you for more information here.`,
      );
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
      const [toDecide, toRespond, mine] = await Promise.all([
        this.claims.toDecide(),
        this.claims.toRespond(),
        this.claims.mine(),
      ]);
      this.toDecide.set(toDecide);
      this.toRespond.set(toRespond);
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
