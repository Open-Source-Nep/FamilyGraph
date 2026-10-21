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

## Acceptance criteria

- A private profile's name and personal details are not exposed in public views.
- A private person can remain represented as a `Private` node in a public family graph.
- A deleted person can remain represented as a `Deleted` node.
- Public graph traversal can pass through active relationships involving placeholders.
- Inactive relationships are not exposed as active merely because profile privacy is enabled.

## Open details

- Whether a relationship category may be shown next to a placeholder is undecided; default to avoiding unnecessary disclosure.
- Exact meaning and reversibility of profile deletion need further definition.
- Retention rules for personal data versus audit metadata need further definition.
