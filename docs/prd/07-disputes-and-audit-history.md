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

## Acceptance criteria

- `AC-07.1` An authorized reviewer can reconstruct the lifecycle of a relationship from its audit events.
- `AC-07.2` An inactive relationship does not appear as active merely because it exists in audit history.
- `AC-07.3` Claim approval/rejection records are distinct from relationship consent events.
- `AC-07.4` Sensitive evidence is not exposed through public graph views or general audit access.

## Open details

- Exact audit visibility by role.
- Retention period and any legally required deletion exceptions.
- Whether contributors can see the full history of relationships they no longer maintain.
- Abuse-reporting and contributor-suspension workflows.
