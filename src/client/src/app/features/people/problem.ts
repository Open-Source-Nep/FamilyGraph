import { HttpErrorResponse } from '@angular/common/http';

import { localNumber } from '../../core/i18n/local-format';
import { maxNamePartLength } from './person';

/** Wording for the server's error codes (ProblemDetails `code`, D-010). */
const messages: Record<string, string> = {
  'person.first_name_required': $localize`:@@problem.person.firstNameRequired:Enter a first name.`,
  'person.not_maintainer': $localize`:@@problem.person.notMaintainer:Only the maintainer of this profile can change it.`,
  'person.not_found': $localize`:@@problem.person.notFound:This profile does not exist.`,
  'link.not_endpoint_maintainer': $localize`:@@problem.link.notEndpointMaintainer:You can only add a link when you maintain the parent or the child.`,
  'link.duplicate': $localize`:@@problem.link.duplicate:These two people are already linked as parent and child.`,
  'link.self_link': $localize`:@@problem.link.selfLink:A person cannot be their own parent.`,
  'link.cycle_detected': $localize`:@@problem.link.cycleDetected:The child is already an ancestor of the parent, so this link would form a loop.`,
  'link.cycle_unverifiable': $localize`:@@problem.link.cycleUnverifiable:This link could not be checked for loops because the family tree above the parent is too large.`,
  'link.not_found': $localize`:@@problem.link.notFound:This link does not exist.`,
  'link.consent_not_maintainer': $localize`:@@problem.link.consentNotMaintainer:Only the maintainer of this profile can change its approval.`,
  'link.consent_unchanged': $localize`:@@problem.link.consentUnchanged:This link already has that approval. Reload the page to see its state.`,
  'link.sole_maintainer_must_delete': $localize`:@@problem.link.soleMaintainerMustDelete:You maintain both people on this link, so delete it instead of declining or withdrawing it.`,
  'link.not_sole_maintainer': $localize`:@@problem.link.notSoleMaintainer:Only someone who maintains both people can delete this link. Withdraw your approval instead.`,
  'link.deleted': $localize`:@@problem.link.deleted:This link has been deleted. Reload the page to see the current links.`,
  'handover.not_found': $localize`:@@problem.handover.notFound:This handover link does not exist. Check that you have the whole link.`,
  'handover.closed': $localize`:@@problem.handover.closed:This handover link was already used or cancelled. Ask the maintainer for a new one.`,
  'handover.expired': $localize`:@@problem.handover.expired:This handover link has expired. Ask the maintainer for a new one.`,
  'handover.own_profile': $localize`:@@problem.handover.ownProfile:You already maintain this profile.`,
  'handover.already_open': $localize`:@@problem.handover.alreadyOpen:Another handover link was just created. Reload the page.`,
  'person.deleted': $localize`:@@problem.person.deleted:This profile was deleted.`,
  'person.no_image': $localize`:@@problem.person.noImage:This profile has no photo. Reload the page.`,
  'profile_image.too_large': $localize`:@@problem.profileImage.tooLarge:This photo is too large. Choose a smaller one.`,
  'profile_image.unsupported_format': $localize`:@@problem.profileImage.unsupportedFormat:Choose a JPEG, PNG or WebP photo.`,
  'accounts.not_administrator': $localize`:@@problem.accounts.notAdministrator:Only an administrator can see the accounts.`,
  'account.not_found': $localize`:@@problem.account.notFound:This account does not exist. Reload the page.`,
  'transfer.not_administrator': $localize`:@@problem.transfer.notAdministrator:Only an administrator can transfer profiles.`,
  'transfer.same_maintainer': $localize`:@@problem.transfer.sameMaintainer:That account already maintains this profile.`,
  'transfer.same_account': $localize`:@@problem.transfer.sameAccount:Choose a different account to move the profiles to.`,
  'graph.timeout': $localize`:@@problem.graph.timeout:The family graph took too long to load. Please try again.`,
};

/** The wording when nothing more specific is known. */
export function somethingWentWrong(): string {
  return $localize`:@@problem.unknown:Something went wrong. Please try again.`;
}

export function problemMessage(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    const code: unknown = error.error?.code;
    if (typeof code === 'string' && code in messages) {
      return messages[code];
    }
    // Validation problems list the invalid fields; the only API-level rule is the length limit.
    if (error.status === 400 && error.error?.errors) {
      return $localize`:@@problem.namePartTooLong:Each part of the name can be at most ${localNumber(maxNamePartLength)}:max: characters.`;
    }
  }
  return somethingWentWrong();
}
