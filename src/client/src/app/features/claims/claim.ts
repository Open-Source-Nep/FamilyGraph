import { Person } from '../people/person';

/** Where a claim stands (D-051). */
export type ClaimState = 'Open' | 'Approved' | 'Rejected' | 'Withdrawn' | 'Cancelled';

/** The longest note a claim may carry (D-051). */
export const maxEvidenceLength = 2000;

/**
 * A claim as you may see it (D-051, D-052). The note is sent to the claimant, the deciding
 * maintainer and administrators; the administrator's question to the claimant and administrators;
 * the claimed person's reply to that maintainer and administrators; the rationale, `profileClaimed`
 * and `openClaimsOnProfile` to administrators only.
 */
export interface Claim {
  id: string;
  person: Person;
  state: ClaimState;
  evidence: string | null;
  submittedAt: string;
  closedAt: string | null;
  /** An open claim waits for administrators rather than the maintainer. */
  withAdministrators: boolean;
  /** The claimant appealed the maintainer's rejection. */
  appealed: boolean;
  decidedByAdministrator: boolean;
  administratorQuestion: string | null;
  maintainerResponse: string | null;
  rationale: string | null;
  /** You, the claimant, may ask administrators to review it now. */
  mayRequestReview: boolean;
  /** While the maintainer decides: from when you may ask administrators instead. */
  reviewAvailableAt: string | null;
  profileClaimed: boolean | null;
  openClaimsOnProfile: number | null;
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
  /** Your latest claim on this profile, open or closed. */
  yourClaim: Claim | null;
  /** After an administrator rejected your claim: when you may claim this profile again. */
  claimAgainFrom: string | null;
  /** The claims you may decide as maintainer. */
  toDecide: Claim[];
  /** Claims on this claimed profile you maintain, which you may reply to. */
  toRespond: Claim[];
  /** Open claims on your profile wait for an administrator. */
  needsAdministrator: boolean;
}
