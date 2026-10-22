# PRD 10 — Open Decisions Register

This file is intentionally unresolved. Claude Code must not silently invent decisions listed here. Update each item when the product owner decides it.

## People and accounts
- **Decided (2026-10-21):** profiles of a deleted account are assigned to one administrator account (the first on the configured list with an existing account; they move on if that administrator is removed). A claimed person may instead delete their own profile. Recorded consent is unchanged. See PRD 01.
- **Decided (2026-10-21):** an administrator may grant a succession request for a profile inherited by an administrator or whose maintainer is unavailable. A claimed person's own profile changes maintainer only through a claim.

## Relationships
- **Decided (initial invariant, 2026-10-21):** a person cannot be their own parent. A direct two-person cycle (A parent of B and B parent of A) is rejected.
- **Decided (2026-10-21):** deeper cycles (A → B → C → A) are rejected when a link is created or reactivated, if the proposed child is already an ancestor of the proposed parent. The check counts active, pending, and inactive links (not deleted ones). If the bounded check hits its limit, the link is rejected with an explanation that it could not be verified as cycle-free. Rejected alternatives: allow and flag for review; defer to the database/traversal decisions. See PRD 02.
- **Decided (2026-10-21):** duplicate or contradictory relationships from a merge are resolved by the reviewers choosing one link to keep. See PRD 06.
- **Decided (2026-10-21):** at most one parent–child link per ordered pair (parent, child), so the same pair cannot have both a biological and an adoptive link.
- **Decided (2026-10-21):** no limit on the number of parents, of either type.
- **Decided (2026-10-22):** a parent's children have a birth order for display only, set manually per parent by the parent's maintainer, with no consent and no birth dates. New children are unordered until placed. Rejected alternatives: an optional birth date on Person; letting the child's maintainer reorder; child-side approval of order changes. See PRD 02.

## Claims and identity
- **Decided (2026-10-21):** a maintainer is unavailable after 30 days without response (configurable) or when their account no longer exists. See PRD 04.
- **Decided (2026-10-21):** parallel claims are allowed; with more than one open claim, an administrator decides them together.
- **Decided (2026-10-21):** one appeal per claim; after an administrator rejection, a new claim is allowed after 90 days (configurable) with new evidence. Claims on already-claimed profiles go to an administrator, and the current maintainer is notified.
- **Decided (2026-10-21):** evidence is visible to the claimant, reviewing administrators and the deciding maintainer, and is deleted 90 days after the final decision (configurable). Audit keeps the decision and rationale.

## Privacy and deletion
- **Decided (2026-10-21):** deletion permanently erases personal data and leaves a `Deleted` placeholder. It is not reversible. The maintainer or an administrator may delete. No new links to a deleted profile. See PRD 05.
- **Decided (2026-10-21):** audit history keeps event types, timestamps, account, profile and relationship IDs. Erased personal data is replaced with `[erased]`.
- **Decided (2026-10-21):** a relationship's biological or adoptive type may be shown next to a placeholder.
- **Decided (2026-10-21):** abandonment changes nothing automatically.
- **Decided (2026-10-21):** profiles are Public (default) or Private. The graph is visible only to signed-in users. Maintainers and administrators see through `Private`.

## Merging
- **Decided (2026-10-21):** anyone may propose a merge; each profile's maintainer approves. An administrator may stand in only for an unavailable maintainer of an unclaimed profile. Two claimed profiles are never merged. See PRD 06.
- **Decided (2026-10-21):** moved links keep their approvals; reviewers choose which of two clashing links to keep; merges that would create a self-link or cycle are blocked.
- **Decided (2026-10-21):** merges are not reversible.

## Audit and moderation
- **Decided (2026-10-21):** relationship audit history is visible to the current maintainers of both profiles and administrators. See PRD 07.
- **Decided (2026-10-21):** audit records are kept indefinitely; personal data in them is erased on profile deletion.
- **Decided (2026-10-21):** signed-in users can report abuse to an administrator queue. Administrators can suspend accounts; suspension blocks sign-in and changes but leaves profiles, relationships and consent untouched.

## Graph exploration
- **Decided (2026-10-21):** show the shortest path, with "There are more connections between these people" listing up to 5 more (configurable). See PRD 08.
- **Decided (2026-10-21):** no derived relationship labels in the first release; explanations show the path. Admin-defined label rules are planned for later.
- **Decided (2026-10-21):** unknown parents are ordinary profiles with the first name `UNKNOWN`; no special state.
- **Decided (2026-10-22):** a local view shows the people nearest to the centre in any direction, closest first, rather than only ancestors and descendants. See PRD 08.
- **Decided (2026-10-21):** adoptive steps read "adoptive parent"/"adopted child"; adoptive links are drawn differently with a legend.

## General rule
When work depends on an unresolved item:
1. Explain why the decision matters.
2. Offer a small set of options with consequences.
3. Ask the product owner before implementing behavior that depends on it.
