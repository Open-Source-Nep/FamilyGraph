import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { DigitsPipe } from '../../core/i18n/local-format.pipes';
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
  imports: [DigitsPipe, RouterLink],
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
      {
        title: $localize`:@@approvals.waitingForYou:Waiting for your approval`,
        links: pending,
        yours: true,
      },
      {
        title: $localize`:@@approvals.withdrawnByYou:Declined or withdrawn by you`,
        links: withdrawn,
        yours: true,
      },
      {
        title: $localize`:@@approvals.waitingForOthers:Waiting for others`,
        links: others,
        yours: false,
      },
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
      const labels = consentLabels(own, other);
      return own.approval === 'Pending'
        ? [
            { label: labels.approve, endpoint, approve: true },
            ...(other.maintainedByYou ? [] : [{ label: labels.decline, endpoint, approve: false }]),
          ]
        : [{ label: labels.restore, endpoint, approve: true }];
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
      .map(([endpoint, own]) => ({ label: approveAsAdministratorLabel(own), side: endpoint }));
  }

  /** The pending sides of a marriage that someone else maintains (D-040). */
  protected marriageAdministratorActions(marriage: MarriageConsent): AdministratorAction[] {
    return [marriage.first, marriage.second]
      .filter((own) => own.approval === 'Pending' && !own.maintainedByYou && !own.placeholder)
      .map((own) => ({ label: approveAsAdministratorLabel(own), side: own.id }));
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
      const what = describe(updated);
      this.notice.set(
        updated.isActive
          ? $localize`:@@approvals.adminApprovedActive:You approved ${what}:link: as administrator. It is now active.`
          : $localize`:@@approvals.adminApprovedWaiting:You approved ${what}:link: as administrator. It still waits for the other side.`,
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
          ? $localize`:@@approvals.adminApprovedMarriageActive:You approved the marriage of ${what}:spouses: as administrator. It is now active.`
          : $localize`:@@approvals.adminApprovedMarriageWaiting:You approved the marriage of ${what}:spouses: as administrator. It still waits for the other side.`,
      );
    });
  }

  /** Someone who maintains both people may delete the link in any consent state (D-014). */
  protected deletable(link: LinkConsent): boolean {
    return link.parent.maintainedByYou && link.child.maintainedByYou;
  }

  /** Why a link with no action for you is inactive. */
  protected status(link: LinkConsent): string {
    return waitingStatus(link.parent.maintainedByYou ? link.child : link.parent);
  }

  protected async act(link: LinkConsent, action: ConsentAction): Promise<void> {
    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);
    try {
      const updated = action.approve
        ? await this.relationships.approve(link.id, action.endpoint)
        : await this.relationships.disagree(link.id, action.endpoint);
      const what = describe(updated);
      this.notice.set(
        updated.isActive
          ? $localize`:@@approvals.linkActive:${what}:link: is now active.`
          : action.approve
            ? $localize`:@@approvals.approvedLink:You approved ${what}:link:. It becomes active when the other maintainer approves.`
            : $localize`:@@approvals.declinedLink:You declined ${what}:link:. You can restore your approval later.`,
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
      this.notice.set($localize`:@@approvals.deletedLink:You deleted ${describe(link)}:link:.`);
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
      const labels = consentLabels(own, other);
      return own.approval === 'Pending'
        ? [
            { label: labels.approve, spouseId: own.id, approve: true },
            ...(other.maintainedByYou
              ? []
              : [{ label: labels.decline, spouseId: own.id, approve: false }]),
          ]
        : [{ label: labels.restore, spouseId: own.id, approve: true }];
    });
  }

  protected marriageDeletable(marriage: MarriageConsent): boolean {
    return marriage.first.maintainedByYou && marriage.second.maintainedByYou;
  }

  /** Why a marriage with no action for you is inactive. */
  protected marriageStatus(marriage: MarriageConsent): string {
    return waitingStatus(marriage.first.maintainedByYou ? marriage.second : marriage.first);
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
          ? $localize`:@@approvals.marriageActive:The marriage of ${what}:spouses: is now active.`
          : action.approve
            ? $localize`:@@approvals.approvedMarriage:You approved the marriage of ${what}:spouses:. It becomes active when the other maintainer approves.`
            : $localize`:@@approvals.declinedMarriage:You declined the marriage of ${what}:spouses:. You can restore your approval later.`,
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
      this.notice.set(
        $localize`:@@approvals.deletedMarriage:You deleted the marriage of ${describeMarriage(marriage)}:spouses:.`,
      );
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
  return $localize`:@@approvals.spouses:${fullName(marriage.first)}:first: and ${fullName(marriage.second)}:second:`;
}

/**
 * The button labels for your side. When you maintain both people, each side's buttons name whose
 * side they are for.
 */
function consentLabels(
  own: LinkEndpointConsent,
  other: LinkEndpointConsent,
): { approve: string; decline: string; restore: string } {
  const name = fullName(own);
  return {
    approve: other.maintainedByYou
      ? $localize`:@@approvals.approveFor:Approve for ${name}:name:`
      : $localize`:@@approvals.approve:Approve`,
    decline: $localize`:@@approvals.decline:Decline`,
    restore: other.maintainedByYou
      ? $localize`:@@approvals.restoreFor:Restore approval for ${name}:name:`
      : $localize`:@@approvals.restore:Restore approval`,
  };
}

function approveAsAdministratorLabel(own: LinkEndpointConsent): string {
  return $localize`:@@approvals.approveAsAdministrator:Approve for ${fullName(own)}:name: as administrator`;
}

/** Why a link or marriage with no action for you is inactive. */
function waitingStatus(other: LinkEndpointConsent): string {
  const name = fullName(other);
  return other.approval === 'Disagreed'
    ? $localize`:@@approvals.otherDeclined:The maintainer of ${name}:name: declined or withdrew approval.`
    : $localize`:@@approvals.waitingForMaintainer:Waiting for the maintainer of ${name}:name: to approve.`;
}

function hasOwn(link: LinkConsent, approval: EndpointApproval): boolean {
  return [link.parent, link.child].some(
    (endpoint) => endpoint.maintainedByYou && endpoint.approval === approval,
  );
}

/** "Hari as adoptive father of Maya", for messages and list items. */
function describe(link: LinkConsent): string {
  const role = relationWord('parent', link.parent.gender, link.type);
  return $localize`:@@approvals.describeLink:${fullName(link.parent)}:parent: as ${role}:role: of ${fullName(link.child)}:child:`;
}
