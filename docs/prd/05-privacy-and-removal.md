# PRD 05 — Privacy and Removal

## Purpose

Minimize personal data while allowing public exploration of family relationships.

## Requirements

- Public visibility of a relationship does not automatically make the connected profiles public.
- A private profile displays a generic `Private` placeholder.
- A deleted profile displays a generic `Deleted` placeholder.
- Placeholders must not expose hidden names or personal details.
- Keep relationships visible and available for public graph traversal even when an endpoint is private or deleted, provided the relationship itself is active under PRD 03.
- A profile becoming private or deleted does not by itself deactivate or delete its relationships.
- Profile maintainers control profile privacy settings.
- Relationship removal follows PRD 03; it is not a separate unrestricted visitor action.
- Preserve an appropriate audit trail for privacy changes, corrections, removals, and challenges.
- Minimize personal data collected and stored.

## Visibility

- The graph is visible only to signed-in users. Anonymous visitors see no profiles or relationships.
- Each profile is either **Public** or **Private**. New profiles start Public.
- A Public profile shows its name to signed-in users.
- A Private profile shows the `Private` placeholder to everyone except its maintainer and administrators.
- A relationship's type (biological or adoptive) may be shown next to a `Private` or `Deleted` placeholder, so adoption stays visible along paths (PRD 02).

## Profile deletion

- A profile's maintainer, or an administrator for moderation, may delete it. The audit history records which of the two did it.
- Deletion permanently erases the profile's personal data and cannot be undone. The profile remains as a `Deleted` placeholder.
- Existing active relationships of a deleted profile stay active and traversable. No new relationship can be created with a deleted profile.
- After erasure, audit history keeps event types, timestamps, acting account IDs, profile IDs and relationship IDs. Erased personal data in earlier audit records is replaced with `[erased]`.

## Abandoned accounts and profiles

- Inactivity alone never changes a profile's visibility, maintainer or deletion state. Handover is covered by maintainer succession (PRD 01, PRD 10) and claims (PRD 04).

## Acceptance criteria

- `AC-05.1` A private profile's name and personal details are not exposed in public views.
- `AC-05.2` A private person can remain represented as a `Private` node in a public family graph.
- `AC-05.3` A deleted person can remain represented as a `Deleted` node.
- `AC-05.4` Public graph traversal can pass through active relationships involving placeholders.
- `AC-05.5` Inactive relationships are not exposed as active merely because profile privacy is enabled.
- `AC-05.6` Anonymous visitors cannot view profiles or relationships.
- `AC-05.7` A new profile is Public by default, and its maintainer can make it Private.
- `AC-05.8` A Private profile's name is visible to its maintainer and administrators only.
- `AC-05.9` A relationship's biological or adoptive type is shown next to `Private` and `Deleted` placeholders.
- `AC-05.10` Deleting a profile permanently erases its personal data, including from earlier audit records, and leaves a `Deleted` placeholder.
- `AC-05.11` A profile can be deleted by its maintainer or an administrator, and the audit history distinguishes the two.
- `AC-05.12` A new relationship cannot be created with a deleted profile.

## Open details

- Retention periods for audit records are covered by PRD 07 and PRD 10.
