import { Component, computed, inject, input, output, signal } from '@angular/core';

import { problemMessage } from '../people/problem';
import { AccountSummary, accountLabel } from './admin';
import { AdminService } from './admin.service';

/**
 * Lets an administrator make another account a profile's maintainer, without that account
 * accepting (D-035). The accounts load only when the administrator opens the box.
 */
@Component({
  selector: 'app-person-transfer',
  templateUrl: './person-transfer.html',
})
export class PersonTransfer {
  private readonly admin = inject(AdminService);

  readonly personId = input.required<string>();
  /** Emits after a transfer, so the page can reload who maintains the profile. */
  readonly transferred = output<void>();

  protected readonly accounts = signal<AccountSummary[] | null>(null);
  protected readonly target = signal('');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly message = signal<string | null>(null);
  protected readonly accountLabel = accountLabel;
  protected readonly targetAccount = computed(() =>
    this.accounts()?.find((account) => account.id === this.target()),
  );

  protected async open(): Promise<void> {
    await this.run(async () => {
      this.accounts.set((await this.admin.listAccounts()).accounts);
    });
  }

  protected async transfer(): Promise<void> {
    const to = this.targetAccount();
    if (!to) {
      return;
    }
    await this.run(async () => {
      await this.admin.transferProfile(this.personId(), to.id);
      this.message.set($localize`:@@transfer.done:This profile now belongs to ${to.email}:email:.`);
      this.target.set('');
      this.transferred.emit();
    });
  }

  private async run(action: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    this.message.set(null);
    try {
      await action();
    } catch (error) {
      this.error.set(problemMessage(error));
    } finally {
      this.busy.set(false);
    }
  }
}
