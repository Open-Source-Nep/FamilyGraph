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

## Claiming a profile (agreed 2026-10-23 and 2026-11-04)

- Any signed-in account may claim a profile it does not maintain, from the person page, with an optional note.
- A maintainer may instead mark a profile they maintain as themselves ("This is me"). It needs no approval and is recorded as a self-approved identity claim.
- An account is at most one person: it has at most one claimed profile, and cannot claim again while it has one or an open claim.
- A claimant may withdraw an open claim, which frees their account to claim again.
- A deleted profile cannot be claimed. Deleting a profile cancels its open claims; deleting a claimed profile frees its claimant's account.
- A claimed profile is maintained only by the claimed person: it cannot be handed over or transferred (PRD 01), and no administrator approves its relationships on its behalf (PRD 03).
- Who is claimed is told only to the profile's maintainer, administrators and the claimed person.
- Claims waiting for a maintainer are listed on their Approvals page and on the person page; the claimant sees their claims on both pages too.

## Unavailability, competing claims and appeals

- The original maintainer is **unavailable** when they have not responded to a claim within 30 days (configurable per instance), or when their account no longer exists. The claimant may then go to manual review.
- **Parallel claims** to the same profile are allowed. While a profile has more than one open claim, the maintainer cannot decide any of them; all go to an administrator, who decides them together. With a single open claim, the maintainer decides as usual.
- **Appeals:** a rejected claim may be appealed once to an administrator. After an administrator rejects it, the claimant may submit a new claim for the same profile after 90 days (configurable), with new evidence.
- **Already-claimed profiles** may be claimed by someone else only through manual review. The current maintainer is notified and may respond, but does not decide the claim.

## Administrator review (agreed 2026-11-04)

- A claimant may ask administrators to review a claim: once, after the maintainer's rejection (an appeal), or when the maintainer has not decided an open claim within the response time. An administrator's decision is final; after a rejection the claimant waits for the waiting period and claims again.
- Administrators review claims sent to them, competing claims, and claims on claimed profiles on their own Claims page. They may ask the claimant a question; the claimant answers by changing their note. The maintainer of a claimed profile may add one reply for the administrators.
- Every administrator decision needs a written reason. Approving one of competing claims rejects the others with the same reason.
- The question, the note and the reply are erased together with the evidence. An appeal is possible only until then.

## Evidence

- Evidence is one optional free-text note of at most 2,000 characters: who the claimant is and who can confirm it. No files (agreed 2026-11-04).
- Submitted evidence is visible only to the claimant, the administrators reviewing the claim, and, for a claim the maintainer decides, that maintainer.
- Evidence is deleted 90 days after the claim's final decision (configurable).
- Audit history keeps the claim, its decision and the administrator's rationale, but not the evidence. The rationale stays on the claim and is shown to administrators only (proposed 2026-11-04, D-052).

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
- `AC-04.11` A maintainer can mark a profile they maintain as themselves without approval; it is audited as a self-approved identity claim.
- `AC-04.12` An account has at most one claimed profile, and cannot claim again while it has one or an open claim.
- `AC-04.13` A claimant can withdraw an open claim.
- `AC-04.14` A claim's evidence is one optional note of at most 2,000 characters.
- `AC-04.15` A deleted profile cannot be claimed, and deleting a profile cancels its open claims.

## Open details

- Notification is in the app only for now (proposed 2026-11-04, see D-051): no emails.
