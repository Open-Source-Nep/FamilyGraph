# PRD 06 — Duplicate Detection and Merging

## Purpose

Allow the community to resolve duplicate profiles without making unsafe identity assumptions or bypassing consent.

## Requirements

- Suggest possible matching profiles during person creation.
- A name match alone must never establish identity.
- Allow duplicates when identity is uncertain.
- Merges require review; do not merge automatically.
- Merging must preserve relevant relationship history, contributors, claim history, and audit records.
- Never merge claimed profiles or transfer ownership merely because someone requests it.
- Merging must not bypass relationship-consent rules.
- If two profiles represent the same real person, the merge process must reconcile their graph connections without silently activating relationships that lack required consent.
- Record who reviewed/approved a merge and when.

## Acceptance criteria

- `AC-06.1` Similar names can trigger a suggestion but never an automatic merge.
- `AC-06.2` Users can keep profiles separate when identity is uncertain.
- `AC-06.3` Merge actions are auditable.
- `AC-06.4` A merge cannot silently transfer a claimed profile to another account.
- `AC-06.5` Relationships affected by a merge continue to obey PRD 03.

## Open details

- Who can initiate and approve merges, particularly when either profile is claimed.
- Whether both claimants must consent to a merge or whether an administrator can resolve it.
- How to handle conflicting relationships, duplicate edges, and cycles created by a merge.
- Whether a merge can be reversed and how post-merge changes would be reconciled.
