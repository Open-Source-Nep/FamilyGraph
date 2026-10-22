import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { fullName } from '../people/person';
import { problemMessage } from '../people/problem';
import {
  EndpointApproval,
  LinkConsent,
  LinkConsentList,
  LinkEndpoint,
  LinkEndpointConsent,
} from './relationship';
import { RelationshipsService } from './relationships.service';

/** A consent change the signed-in user may make on one endpoint of a link. */
interface ConsentAction {
  label: string;
  endpoint: LinkEndpoint;
  approve: boolean;
}

/**
 * The inactive links of the profiles you maintain (PRD 03, D-021): approve or decline those
 * waiting for you, restore an approval you withdrew, and see which wait for someone else.
 */
@Component({
  selector: 'app-approvals-page',
  imports: [RouterLink],
  templateUrl: './approvals-page.html',
})
export class ApprovalsPage {
  private readonly relationships = inject(RelationshipsService);

  protected readonly fullName = fullName;
  protected readonly list = signal<LinkConsentList | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);
  protected readonly saving = signal(false);

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

  private load(): void {
    this.relationships.listInactive().then(
      (list) => this.list.set(list),
      (error: unknown) => this.error.set(problemMessage(error)),
    );
  }
}

function hasOwn(link: LinkConsent, approval: EndpointApproval): boolean {
  return [link.parent, link.child].some(
    (endpoint) => endpoint.maintainedByYou && endpoint.approval === approval,
  );
}

/** "Hari as adoptive parent of Maya", for messages and list items. */
function describe(link: LinkConsent): string {
  const role = link.type === 'Adoptive' ? 'adoptive parent' : 'parent';
  return `${fullName(link.parent)} as ${role} of ${fullName(link.child)}`;
}
