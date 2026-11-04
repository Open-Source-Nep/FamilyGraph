import { Person } from '../people/person';

/** Where a claim stands (D-051). */
export type ClaimState = 'Open' | 'Approved' | 'Rejected' | 'Withdrawn' | 'Cancelled';

/** The longest note a claim may carry (D-051). */
export const maxEvidenceLength = 2000;

/** A claim as you may see it. The note is only sent to the claimant and the deciding maintainer. */
export interface Claim {
  id: string;
  person: Person;
  state: ClaimState;
  evidence: string | null;
  submittedAt: string;
  closedAt: string | null;
}

export interface ClaimList {
  claims: Claim[];
  isTruncated: boolean;
}

/** One profile's claim state for you, from `GET /api/people/{id}/claims`. */
export interface ProfileClaimStatus {
  /** Whether the profile is claimed; null unless you maintain it or are an administrator. */
  claimed: boolean | null;
  claimedByYou: boolean;
  /** Your account already has a claimed profile or an open claim, so it cannot claim another. */
  youHaveClaim: boolean;
  yourClaim: Claim | null;
  /** The claims you may decide as maintainer. */
  toDecide: Claim[];
  /** Open claims on your profile wait for an administrator. */
  needsAdministrator: boolean;
}
