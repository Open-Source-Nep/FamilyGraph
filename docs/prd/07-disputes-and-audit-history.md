# PRD 07 — Disputes and Audit History

## Purpose

Provide accountability without giving administrators the power to override relationship consent.

## Requirements

- Retain audit events for relationship creation, approval, disagreement, approval restoration, and deletion.
- Retain relevant claim decisions, profile privacy changes, merge decisions, and administrative actions.
- Disagreed relationships remain in audit history even though they are absent from the active graph.
- When a relationship between profiles with the same maintainer is deleted, retain an audit record of its prior existence and deletion.
- Audit history must distinguish identity-claim decisions from relationship-consent decisions.
- Record administrator decisions and rationale for manual claim review.
- Restrict access to sensitive claim evidence and non-public audit details.
- Administrators can investigate disputes but cannot force relationship activation without required consent.

## Audit visibility and retention

- A relationship's audit history is visible to the current maintainers of its two profiles and to administrators. Former maintainers and other users do not see it.
- Claim evidence follows PRD 04.
- Audit records are kept indefinitely. Personal data in them is erased when a profile is deleted (PRD 05), so retained records hold IDs, event types, timestamps and rationale.

## Abuse reporting and suspension

- Any signed-in user can report a profile or relationship with a short reason. Reports go to an administrator queue.
- Administrators can suspend an account. A suspended account cannot sign in or make changes. Administrators can lift a suspension. Suspensions and their lifting are audited.
- Suspension does not change the account's profiles or relationships, including their consent states. Its profiles can be handed over through claims (PRD 04) or maintainer succession. Administrators may still delete abusive profiles individually (PRD 05).

## Acceptance criteria

- `AC-07.1` An authorized reviewer can reconstruct the lifecycle of a relationship from its audit events.
- `AC-07.2` An inactive relationship does not appear as active merely because it exists in audit history.
- `AC-07.3` Claim approval/rejection records are distinct from relationship consent events.
- `AC-07.4` Sensitive evidence is not exposed through public graph views or general audit access.
- `AC-07.5` Only the current maintainers of a relationship's profiles and administrators can view its audit history.
- `AC-07.6` A signed-in user can report a profile or relationship, and the report reaches the administrator queue.
- `AC-07.7` A suspended account cannot sign in or make changes.
- `AC-07.8` Suspending an account does not change its profiles or the consent state of their relationships.

## Open details

- Jurisdiction-specific legal retention or deletion requirements are left to each instance operator.
