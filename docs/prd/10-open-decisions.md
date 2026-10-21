# PRD 10 — Open Decisions Register

This file is intentionally unresolved. Claude Code must not silently invent decisions listed here. Update each item when the product owner decides it.

## People and accounts
- What happens to profiles when an account is deleted?
- Can a maintainer be replaced if their account becomes permanently inaccessible outside the claim process?

## Relationships
- **Decided (initial invariant, 2026-10-21):** a person cannot be their own parent. A direct two-person cycle (A parent of B and B parent of A) is rejected.
- **Decided (2026-10-21):** deeper cycles (A → B → C → A) are rejected when a link is created or reactivated, if the proposed child is already an ancestor of the proposed parent. The check counts active, pending, and inactive links (not deleted ones). If the bounded check hits its limit, the link is rejected with an explanation that it could not be verified as cycle-free. Rejected alternatives: allow and flag for review; defer to the database/traversal decisions. See PRD 02.
- What happens when a merge produces duplicate or contradictory relationships?
- **Decided (2026-10-21):** at most one parent–child link per ordered pair (parent, child), so the same pair cannot have both a biological and an adoptive link.
- **Decided (2026-10-21):** no limit on the number of parents, of either type.

## Claims and identity
- How is original-maintainer unavailability determined?
- How are competing claims to one profile handled?
- What is the retry/appeal policy after a claim rejection?
- How long is submitted evidence retained, and who can access it?

## Privacy and deletion
- **Decided (2026-10-21):** deletion permanently erases personal data and leaves a `Deleted` placeholder. It is not reversible. The maintainer or an administrator may delete. No new links to a deleted profile. See PRD 05.
- **Decided (2026-10-21):** audit history keeps event types, timestamps, account, profile and relationship IDs. Erased personal data is replaced with `[erased]`.
- **Decided (2026-10-21):** a relationship's biological or adoptive type may be shown next to a placeholder.
- **Decided (2026-10-21):** abandonment changes nothing automatically.
- **Decided (2026-10-21):** profiles are Public (default) or Private. The graph is visible only to signed-in users. Maintainers and administrators see through `Private`.

## Merging
- Who can initiate and approve a merge involving claimed profiles?
- Is consent from both claimed profile owners mandatory?
- Can merges be reversed? If so, how are subsequent edits handled?

## Audit and moderation
- Which roles can see relationship audit history?
- What are the retention periods for audit records?
- Can administrators suspend contributors for abuse, and what effect does suspension have on existing contributions?

## Graph exploration
- How should multiple paths between the same two people be presented?
- Which derived relationship types are in the initial release?
- How are unknown or undisclosed parents represented, if at all?

## General rule
When work depends on an unresolved item:
1. Explain why the decision matters.
2. Offer a small set of options with consequences.
3. Ask the product owner before implementing behavior that depends on it.
