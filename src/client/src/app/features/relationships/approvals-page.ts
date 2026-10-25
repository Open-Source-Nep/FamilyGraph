import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { fullName } from '../people/person';
import { problemMessage } from '../people/problem';
import { MarriagesService } from './marriages.service';
import {
  EndpointApproval,
  LinkConsent,
  LinkConsentList,
  LinkEndpoint,
  LinkEndpointConsent,
  MarriageConsent,
  MarriageConsentList,
  relationWord,
} from './relationship';
import { RelationshipsService } from './relationships.service';

/** A consent change the signed-in user may make on one endpoint of a link. */
interface ConsentAction {
  label: string;
  endpoint: LinkEndpoint;
  approve: boolean;
}

/** A consent change the signed-in user may make for one spouse of a marriage. */
interface MarriageAction {
  label: string;
  spouseId: string;
  approve: boolean;
}

/** A side an administrator may approve on behalf of its maintainer (D-040). */
interface AdministratorAction {
  label: string;
  /** The link endpoint, or the spouse's ID for a marriage. */
  side: string;
}

/**
 * The inactive links of the profiles you maintain (PRD 03, D-021): approve or decline those
 * waiting for you, restore an approval you withdrew, delete a link whose two people you maintain
 * (D-014), and see which wait for someone else. Administrators also see every link and marriage
 * waiting for another maintainer and may approve those sides on their behalf (D-040).
 */
@Component({
  selector: 'app-approvals-page',
  imports: [RouterLink],
  templateUrl: './approvals-page.html',
})
export class ApprovalsPage {
  private readonly relationships = inject(RelationshipsService);
  private readonly marriages = inject(MarriagesService);
  private readonly auth = inject(AuthService);

  protected readonly fullName = fullName;
  protected readonly relationWord = relationWord;
  protected readonly list = signal<LinkConsentList | null>(null);
  protected readonly marriageList = signal<MarriageConsentList | null>(null);
  /** Administrators only: links and marriages waiting for other maintainers (D-040). */
  protected readonly pendingLinks = signal<LinkConsentList | null>(null);
  protected readonly pendingMarriages = signal<MarriageConsentList | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);
  protected readonly saving = signal(false);
  /** The link whose deletion waits for confirmation. */
  protected readonly confirming = signal<string | null>(null);

  /** The links grouped by what they wait for; empty groups are left out. */
  protected readonly sections = computed(() => {
    const links = this.list()?.links ?? [];
    const pending = links.filter((link) => hasOwn(link, 'Pending'));
    const withdrawn = links.filter((link) => !hasOwn(link, 'Pending') && hasOwn(link, 'Disagreed'));
    const others = links.filter((link) => !hasOwn(link, 'Pending') && !hasOwn(link, 'Disagreed'));
    return [
      { title: 'Waiting for your approval', links: pending, yours: true },
      { title: 'Declined or withdrawn by you', links: withdrawn, yours: true },
      { title: 'Waiting for others', links: others, yours: false },
    ].filter((section) => section.links.length > 0);
  });

  constructor() {
    this.load();
  }

  protected actions(link: LinkConsent): ConsentAction[] {
    const sides: [LinkEndpoint, LinkEndpointConsent, LinkEndpointConsent][] = [
      ['Parent', link.parent, link.child],
      ['Child', link.child, link.parent],
    ];
    return sides.flatMap(([endpoint, own, other]) => {
      if (!own.maintainedByYou || own.approval === 'Approved') {
        return [];
      }
      // Someone who maintains both people deletes the link instead of declining it (D-013).
      const suffix = other.maintainedByYou ? ` for ${fullName(own)}` : '';
      return own.approval === 'Pending'
        ? [
            { label: `Approve${suffix}`, endpoint, approve: true },
            ...(other.maintainedByYou ? [] : [{ label: 'Decline', endpoint, approve: false }]),
          ]
        : [{ label: `Restore approval${suffix}`, endpoint, approve: true }];
    });
  }

  /** The pending sides of a link that someone else maintains (D-040). */
  protected administratorActions(link: LinkConsent): AdministratorAction[] {
    const sides: [LinkEndpoint, LinkEndpointConsent][] = [
      ['Parent', link.parent],
      ['Child', link.child],
    ];
    return sides
      .filter(([, own]) => own.approval === 'Pending' && !own.maintainedByYou && !own.placeholder)
      .map(([endpoint, own]) => ({
        label: `Approve for ${fullName(own)} as administrator`,
        side: endpoint,
      }));
  }

  /** The pending sides of a marriage that someone else maintains (D-040). */
  protected marriageAdministratorActions(marriage: MarriageConsent): AdministratorAction[] {
    return [marriage.first, marriage.second]
      .filter((own) => own.approval === 'Pending' && !own.maintainedByYou && !own.placeholder)
      .map((own) => ({ label: `Approve for ${fullName(own)} as administrator`, side: own.id }));
  }

  protected async approveAsAdministrator(
    link: LinkConsent,
    action: AdministratorAction,
  ): Promise<void> {
    await this.run(async () => {
      const updated = await this.relationships.approveAsAdministrator(
        link.id,
        action.side as LinkEndpoint,
      );
      this.notice.set(
        updated.isActive
          ? `You approved ${describe(updated)} as administrator. It is now active.`
          : `You approved ${describe(updated)} as administrator. It still waits for the other side.`,
      );
    });
  }

  protected async approveMarriageAsAdministrator(
    marriage: MarriageConsent,
    action: AdministratorAction,
  ): Promise<void> {
    await this.run(async () => {
      const updated = await this.marriages.approveAsAdministrator(marriage.id, action.side);
      const what = describeMarriage(updated);
      this.notice.set(
        updated.isActive
          ? `You approved the marriage of ${what} as administrator. It is now active.`
          : `You approved the marriage of ${what} as administrator. It still waits for the other side.`,
      );
    });
  }

  /** Someone who maintains both people may delete the link in any consent state (D-014). */
  protected deletable(link: LinkConsent): boolean {
    return link.parent.maintainedByYou && link.child.maintainedByYou;
  }

  /** Why a link with no action for you is inactive. */
  protected status(link: LinkConsent): string {
    const other = link.parent.maintainedByYou ? link.child : link.parent;
    return other.approval === 'Disagreed'
      ? `The maintainer of ${fullName(other)} declined or withdrew approval.`
      : `Waiting for the maintainer of ${fullName(other)} to approve.`;
  }

  protected async act(link: LinkConsent, action: ConsentAction): Promise<void> {
    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);
    try {
      const updated = action.approve
        ? await this.relationships.approve(link.id, action.endpoint)
        : await this.relationships.disagree(link.id, action.endpoint);
      this.notice.set(
        updated.isActive
          ? `${describe(updated)} is now active.`
          : action.approve
            ? `You approved ${describe(updated)}. It becomes active when the other maintainer approves.`
            : `You declined ${describe(updated)}. You can restore your approval later.`,
      );
      this.load();
    } catch (error) {
      this.error.set(problemMessage(error));
    } finally {
      this.saving.set(false);
    }
  }

  protected async delete(link: LinkConsent): Promise<void> {
    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);
    try {
      await this.relationships.delete(link.id);
      this.confirming.set(null);
      this.notice.set(`You deleted ${describe(link)}.`);
      this.load();
    } catch (error) {
      this.error.set(problemMessage(error));
    } finally {
      this.saving.set(false);
    }
  }

  /** Inactive marriages, those waiting for you first, as the server sorts them (D-031). */
  protected marriageActions(marriage: MarriageConsent): MarriageAction[] {
    const sides: [LinkEndpointConsent, LinkEndpointConsent][] = [
      [marriage.first, marriage.second],
      [marriage.second, marriage.first],
    ];
    return sides.flatMap(([own, other]) => {
      if (!own.maintainedByYou || own.approval === 'Approved') {
        return [];
      }
      const suffix = other.maintainedByYou ? ` for ${fullName(own)}` : '';
      return own.approval === 'Pending'
        ? [
            { label: `Approve${suffix}`, spouseId: own.id, approve: true },
            ...(other.maintainedByYou
              ? []
              : [{ label: 'Decline', spouseId: own.id, approve: false }]),
          ]
        : [{ label: `Restore approval${suffix}`, spouseId: own.id, approve: true }];
    });
  }

  protected marriageDeletable(marriage: MarriageConsent): boolean {
    return marriage.first.maintainedByYou && marriage.second.maintainedByYou;
  }

  /** Why a marriage with no action for you is inactive. */
  protected marriageStatus(marriage: MarriageConsent): string {
    const other = marriage.first.maintainedByYou ? marriage.second : marriage.first;
    return other.approval === 'Disagreed'
      ? `The maintainer of ${fullName(other)} declined or withdrew approval.`
      : `Waiting for the maintainer of ${fullName(other)} to approve.`;
  }

  protected async actOnMarriage(marriage: MarriageConsent, action: MarriageAction): Promise<void> {
    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);
    try {
      const updated = action.approve
        ? await this.marriages.approve(marriage.id, action.spouseId)
        : await this.marriages.disagree(marriage.id, action.spouseId);
      const what = describeMarriage(updated);
      this.notice.set(
        updated.isActive
          ? `The marriage of ${what} is now active.`
          : action.approve
            ? `You approved the marriage of ${what}. It becomes active when the other maintainer approves.`
            : `You declined the marriage of ${what}. You can restore your approval later.`,
      );
      this.load();
    } catch (error) {
      this.error.set(problemMessage(error));
    } finally {
      this.saving.set(false);
    }
  }

  protected async deleteMarriage(marriage: MarriageConsent): Promise<void> {
    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);
    try {
      await this.marriages.delete(marriage.id);
      this.confirming.set(null);
      this.notice.set(`You deleted the marriage of ${describeMarriage(marriage)}.`);
      this.load();
    } catch (error) {
      this.error.set(problemMessage(error));
    } finally {
      this.saving.set(false);
    }
  }

  private async run(change: () => Promise<void>): Promise<void> {
    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);
    try {
      await change();
      this.load();
    } catch (error) {
      this.error.set(problemMessage(error));
    } finally {
      this.saving.set(false);
    }
  }

  private load(): void {
    this.auth.load().then(
      () => {
        if (this.auth.isAdministrator()) {
          this.loadPending();
        }
      },
      (error: unknown) => this.error.set(problemMessage(error)),
    );
    this.relationships.listInactive().then(
      (list) => this.list.set(list),
      (error: unknown) => this.error.set(problemMessage(error)),
    );
    this.marriages.listInactive().then(
      (list) => this.marriageList.set(list),
      (error: unknown) => this.error.set(problemMessage(error)),
    );
  }

  private loadPending(): void {
    this.relationships.listPending().then(
      (list) => this.pendingLinks.set(list),
      (error: unknown) => this.error.set(problemMessage(error)),
    );
    this.marriages.listPending().then(
      (list) => this.pendingMarriages.set(list),
      (error: unknown) => this.error.set(problemMessage(error)),
    );
  }
}

/** "Ram and Sita", for messages. */
function describeMarriage(marriage: MarriageConsent): string {
  return `${fullName(marriage.first)} and ${fullName(marriage.second)}`;
}

function hasOwn(link: LinkConsent, approval: EndpointApproval): boolean {
  return [link.parent, link.child].some(
    (endpoint) => endpoint.maintainedByYou && endpoint.approval === approval,
  );
}

/** "Hari as adoptive father of Maya", for messages and list items. */
function describe(link: LinkConsent): string {
  const role = relationWord('parent', link.parent.gender, link.type);
  return `${fullName(link.parent)} as ${role} of ${fullName(link.child)}`;
}
