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

## Merge authority

- Any signed-in user may propose merging two profiles.
- The maintainer of each profile must approve. If one account maintains both, it approves alone.
- An administrator may approve in place of a maintainer only when that maintainer is unavailable (as defined in PRD 04) **and** the profile is unclaimed. No one overrides a claimed person's refusal.
- Two claimed profiles are never merged. Their conflicting identity claims are resolved first through manual claim review (PRD 04).
- The merged profile keeps the claimed profile's maintainer, if one is claimed. If neither is claimed, the approvers choose one of the two current maintainers. A merge never transfers a claimed profile.
- The merged profile's maintainer manages its name and privacy afterwards under PRD 01 and PRD 05. The absorbed profile is retired, and audit history records that it was merged into the surviving profile.

## Relationships in a merge

- The absorbed profile's relationships move to the surviving profile and keep their per-endpoint approvals unchanged. The surviving maintainer may then disagree or withdraw under PRD 03.
- If both profiles have a link to the same person, including contradictory biological and adoptive links, the reviewers choose which link to keep. The other is retired and stays in audit history. Approvals are never combined across links, so a merge cannot activate a link that lacks consent.
- If the merge would create a self-link or a cycle (PRD 02), it is blocked until the conflicting link is removed through the normal removal rules. A merge never deletes or deactivates a link by itself.

## Reversal

- Merges cannot be reversed. A wrong merge is corrected by creating a separate profile and re-linking it under the normal consent rules. Audit history keeps the full merge record.

## Acceptance criteria

- `AC-06.1` Similar names can trigger a suggestion but never an automatic merge.
- `AC-06.2` Users can keep profiles separate when identity is uncertain.
- `AC-06.3` Merge actions are auditable.
- `AC-06.4` A merge cannot silently transfer a claimed profile to another account.
- `AC-06.5` Relationships affected by a merge continue to obey PRD 03.
- `AC-06.6` A merge completes only after each profile's maintainer approves, or an administrator approves for an unavailable maintainer of an unclaimed profile.
- `AC-06.7` Two claimed profiles cannot be merged.
- `AC-06.8` When both profiles link to the same person, only the link the reviewers choose remains, with its own approvals unchanged.
- `AC-06.9` A merge that would create a self-link or cycle is blocked.

## Open details

- How duplicate suggestions are ranked and presented is decided with the duplicate-suggestion feature. Profiles named `UNKNOWN` are never suggested as duplicates (PRD 08).
