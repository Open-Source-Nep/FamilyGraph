import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { ProfileList, fullName } from '../people/person';
import { problemMessage } from '../people/problem';
import { AccountSummary, accountLabel } from './admin';
import { AdminService } from './admin.service';

/**
 * The administrator's Users page (D-035): every account with its email, whether Google verified
 * it, whether it is an administrator and how many profiles it maintains. Opening an account lists
 * its profiles; an administrator can move one of them, or all of them, to another account without
 * that account accepting. Relationships and their approvals stay as they are.
 */
@Component({
  selector: 'app-users-page',
  imports: [RouterLink],
  templateUrl: './users-page.html',
  styleUrl: './users-page.css',
})
export class UsersPage {
  private readonly admin = inject(AdminService);

  protected readonly accounts = signal<AccountSummary[]>([]);
  protected readonly isTruncated = signal(false);
  protected readonly loading = signal(true);
  /** The profiles of each opened account, by account ID. */
  protected readonly profiles = signal<Record<string, ProfileList>>({});
  /** The account picked as the receiver, by the account or profile it is for. */
  protected readonly targets = signal<Record<string, string>>({});
  /** The account whose "transfer all" waits for confirmation. */
  protected readonly confirmingAll = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly message = signal<string | null>(null);

  protected readonly fullName = fullName;
  protected readonly accountLabel = accountLabel;
  protected readonly byId = computed(
    () => new Map(this.accounts().map((account) => [account.id, account])),
  );

  constructor() {
    void this.reload();
  }

  /** Every account except `accountId`, which already maintains the profiles. */
  protected receivers(accountId: string): AccountSummary[] {
    return this.accounts().filter((account) => account.id !== accountId);
  }

  protected toggled(account: AccountSummary, open: boolean): void {
    if (open && !this.profiles()[account.id]) {
      void this.loadProfiles(account.id);
    }
  }

  protected pick(key: string, accountId: string): void {
    this.targets.update((targets) => ({ ...targets, [key]: accountId }));
    if (this.confirmingAll() === key) {
      this.confirmingAll.set(null);
    }
  }

  protected async transferProfile(owner: AccountSummary, personId: string): Promise<void> {
    const to = this.byId().get(this.targets()[personId]);
    if (!to) {
      return;
    }
    await this.run(async () => {
      await this.admin.transferProfile(personId, to.id);
      const moved = this.profiles()[owner.id]?.people.find((person) => person.id === personId);
      this.message.set(`${moved ? fullName(moved) : 'The profile'} now belongs to ${to.email}.`);
      await this.refresh([owner.id, to.id]);
    });
  }

  protected async transferAll(owner: AccountSummary): Promise<void> {
    const to = this.byId().get(this.targets()[owner.id]);
    if (!to) {
      return;
    }
    await this.run(async () => {
      const { transferred } = await this.admin.transferAll(owner.id, to.id);
      this.confirmingAll.set(null);
      this.message.set(
        `${transferred} ${transferred === 1 ? 'profile' : 'profiles'} moved from ${owner.email} to ${to.email}.`,
      );
      await this.refresh([owner.id, to.id]);
    });
  }

  private async reload(): Promise<void> {
    try {
      const list = await this.admin.listAccounts();
      this.accounts.set(list.accounts);
      this.isTruncated.set(list.isTruncated);
    } catch (error) {
      this.error.set(problemMessage(error));
    } finally {
      this.loading.set(false);
    }
  }

  /** Reloads the counts and the open profile lists of the accounts a transfer changed. */
  private async refresh(accountIds: string[]): Promise<void> {
    await this.reload();
    const open = accountIds.filter((id) => this.profiles()[id]);
    await Promise.all(open.map((id) => this.loadProfiles(id)));
  }

  private async loadProfiles(accountId: string): Promise<void> {
    try {
      const list = await this.admin.listProfiles(accountId);
      this.profiles.update((profiles) => ({ ...profiles, [accountId]: list }));
    } catch (error) {
      this.error.set(problemMessage(error));
    }
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
