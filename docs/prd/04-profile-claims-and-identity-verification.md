# PRD 04 — Profile Claims and Identity Verification

## Purpose

Let a real person claim their profile without conflating identity verification with relationship consent.

## Claim workflow

- The original contributor/maintainer approves or rejects a claim by default.
- If the contributor rejects the claim or is unavailable, the claimant may appeal for manual administrator verification.
- An approved claimant becomes the profile maintainer and gains control over their profile information and privacy settings.
- Claiming does not automatically delete relationships or deactivate them.
- After claiming, the person can disagree with existing relationships under PRD 03.
- Identity verification and relationship consent are separate processes.

## Unavailability, competing claims and appeals

- The original maintainer is **unavailable** when they have not responded to a claim within 30 days (configurable per instance), or when their account no longer exists. The claimant may then go to manual review.
- **Parallel claims** to the same profile are allowed. While a profile has more than one open claim, the maintainer cannot decide any of them; all go to an administrator, who decides them together. With a single open claim, the maintainer decides as usual.
- **Appeals:** a rejected claim may be appealed once to an administrator. After an administrator rejects it, the claimant may submit a new claim for the same profile after 90 days (configurable), with new evidence.
- **Already-claimed profiles** may be claimed by someone else only through manual review. The current maintainer is notified and may respond, but does not decide the claim.

## Evidence

- Submitted evidence is visible only to the claimant, the administrators reviewing the claim, and, for a claim the maintainer decides, that maintainer.
- Evidence is deleted 90 days after the claim's final decision (configurable).
- Audit history keeps the claim, its decision and the administrator's rationale, but not the evidence.

## Manual review

Administrators may:
- Review contextual evidence.
- Consider family-graph context, trusted contributors, and voluntarily supplied evidence.
- Request additional evidence.
- Approve or reject the claim.
- Record the decision and rationale.

Rules:
- Do not require government-issued ID in every case.
- Restrict access to submitted evidence.
- Acknowledge that no verification method guarantees identity.
- Optional independent endorsements may support a decision but are not mandatory in every case.
- An administrator's identity decision does not authorize overriding relationship consent.

## Acceptance criteria

- `AC-04.1` The original maintainer can approve or reject a claim.
- `AC-04.2` Rejection or maintainer unavailability permits an appeal for manual review.
- `AC-04.3` Claim approval transfers profile maintenance to the claimant.
- `AC-04.4` Claim approval alone does not change relationship consent state.
- `AC-04.5` Evidence and administrator rationale are access-controlled.
- `AC-04.6` A claim with no maintainer response for 30 days (configurable), or whose maintainer account no longer exists, can go to manual review.
- `AC-04.7` While a profile has more than one open claim, only an administrator can decide them.
- `AC-04.8` A rejected claim can be appealed once. After an administrator rejection, a new claim for the same profile is accepted only after the waiting period.
- `AC-04.9` A claim on an already-claimed profile is decided only by an administrator, and the current maintainer is notified.
- `AC-04.10` Evidence is deleted after the retention period while the claim decision and rationale remain in audit history.

## Open details

- How maintainers and claimants are notified is a UI/notification decision.
