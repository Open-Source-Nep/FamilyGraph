# PRD 03 — Relationship Consent and Lifecycle

## Purpose

Define when a parent–child relationship is active, inactive, restored, or removed. The same rules apply to marriages (PRD 02), with the spouses as the two endpoints.

## Core rules

- A relationship involving unclaimed profiles is active by default, subject to its maintainer's authority and applicable product rules.
- When both people at the endpoints have claimed profiles, the relationship requires approval from both people to remain active.
- A claimed person can disagree with a relationship involving their own profile. This immediately makes the relationship inactive.
- An inactive relationship is excluded from the public active graph, ordinary relationship views, graph traversal, and derived relationship calculations.
- The disagreement and all lifecycle changes remain in audit history.
- Administrators may investigate disputes but cannot override the mutual-consent requirement. The one exception: an administrator may approve a side that is still pending on behalf of its maintainer (see Administrator approval below).
- Claiming a profile does not automatically deactivate existing relationships. They remain active unless the new maintainer disagrees.
- Track each endpoint's approval/disagreement state independently; do not rely only on a single `Active` boolean.

## Reactivation

- A relationship may reactivate when a disagreement is withdrawn, provided the other party's approval remains valid and has not been withdrawn.
- No fresh confirmation is required from a party whose approval remains valid.
- If both parties have withdrawn approval, the relationship stays inactive until the required approvals are restored.
- The product must define explicit state transitions in implementation without changing these rules.

## Relationship removal

- If both endpoint profiles are maintained by the same user, that maintainer may delete the active relationship. Remove it from the active domain but retain an audit record.
- If endpoint profiles are maintained by different users, a maintainer who wants to remove the relationship withdraws their approval. The relationship becomes inactive rather than being erased from history.
- The requesting party may restore their approval later if the other party's approval remains valid.
- Audit history must distinguish deletion of an active relationship from withdrawal of one party's approval.

## Authority

- A profile maintainer may manage relationship approval for the profile they maintain.
- Once a profile is claimed, the claimant becomes its maintainer.
- No maintainer can override another claimed person's disagreement.
- A visitor or unrelated contributor cannot directly delete another person's relationship.

## Acceptance criteria

- `AC-03.1` Disagreement removes a relationship from the active graph immediately.
- `AC-03.2` Derived relationships dependent on an inactive link are no longer shown as active.
- `AC-03.3` Restoring approval reactivates a relationship when all required approvals are valid.
- `AC-03.4` Historical events remain auditable after deactivation or deletion.
- `AC-03.5` Administrators cannot force an active relationship when required consent is absent: they cannot decline, restore or override a side its maintainer declined or withdrew.
- `AC-03.6` An administrator can approve a pending side of a parent–child link or a marriage on behalf of its maintainer; the relationship becomes active when the other side has approved.
- `AC-03.7` An administrator cannot approve a side its maintainer declined or withdrew, and cannot decline for a maintainer.
- `AC-03.8` An administrator approval is recorded in the audit history apart from a maintainer's own approval, with the administrator and the maintainer it stood in for.
- `AC-03.9` Administrators see every link and marriage waiting for another maintainer on the Approvals page; other users cannot list them.
- `AC-03.10` An administrator cannot approve on behalf of a claimed person.

## Creation decisions (agreed 2026-10-21)

- Only a user who maintains at least one endpoint can create a parent–child link.
- The creator's endpoint(s) start as approved.
- An endpoint maintained by a different user starts as **pending**, whether or not it is claimed. The link stays inactive until that maintainer approves.
- This defines what "subject to its maintainer's authority" means above. A link is active by default only when one user maintains both endpoints.

## Consent transitions (agreed 2026-10-22)

- Only an endpoint's current maintainer can approve or disagree for that endpoint, whether or not the profile is claimed.
- A pending endpoint can approve or decline (disagree).
- A disagreed endpoint can restore its approval. Nothing returns to pending.
- Repeating an action that is already in effect is rejected.
- A user who maintains both endpoints cannot disagree; they delete the link instead.
- See D-013 in `docs/trd/open-decisions.md`.

## Removal decisions (agreed 2026-10-22)

- A user who maintains both endpoints may delete the link whatever its consent state. Deleting never makes a link active.
- Deletion is final. Undoing it means creating the link again.
- A deleted link accepts no further approval, disagreement or deletion.
- See D-014 in `docs/trd/open-decisions.md`.

## Administrator approval (agreed 2026-10-25)

- An administrator may approve a **pending** side of a parent–child link or a marriage on behalf of its maintainer, whoever maintains it. There is no check that the maintainer is unavailable.
- Only pending sides. Declining stays with the maintainer, and a side the maintainer declined or withdrew stays theirs to restore.
- Not for claimed profiles: a claimed person's consent is their own. The check comes with claims (#25); until then every profile is unclaimed.
- The audit history records it as an administrator approval, with the administrator and the maintainer at that time, distinct from the maintainer's own approval.
- The Approvals page shows administrators every link and marriage waiting for another maintainer, each pending side with "Approve as administrator".
- See D-040 in `docs/trd/open-decisions.md`.

## Open details

- Exact event names and state-machine representation are implementation details, provided observable behavior matches these requirements.
- Conflict handling for relationships after profile merges is specified in PRD 06 and may require additional decisions.
