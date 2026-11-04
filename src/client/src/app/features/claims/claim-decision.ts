import { Component, input, output } from '@angular/core';

import { LocalDateTimePipe } from '../../core/i18n/local-format.pipes';
import { Claim } from './claim';

/** One claim waiting for the maintainer: when it came, the claimant's note, and Approve or Reject. */
@Component({
  selector: 'app-claim-decision',
  imports: [LocalDateTimePipe],
  template: `
    <p class="hint" i18n="@@claims.submittedOn">
      Claimed on {{ claim().submittedAt | localDateTime }}.
    </p>
    @if (claim().evidence; as evidence) {
      <blockquote class="claim-note">{{ evidence }}</blockquote>
    } @else {
      <p class="hint" i18n="@@claims.noNote">The claimant left no note.</p>
    }
    <div class="actions">
      <button
        type="button"
        [disabled]="busy()"
        (click)="decided.emit(true)"
        i18n="@@claims.approve"
      >
        Approve: this is them
      </button>
      <button
        type="button"
        class="secondary"
        [disabled]="busy()"
        (click)="decided.emit(false)"
        i18n="@@claims.reject"
      >
        Reject
      </button>
    </div>
  `,
})
export class ClaimDecision {
  readonly claim = input.required<Claim>();
  readonly busy = input(false);
  /** True to approve, false to reject. */
  readonly decided = output<boolean>();
}
