# PRD 10 — Open Decisions Register

This file is intentionally unresolved. Claude Code must not silently invent decisions listed here. Update each item when the product owner decides it.

## People and accounts
- What happens to profiles when an account is deleted?
- Can a maintainer be replaced if their account becomes permanently inaccessible outside the claim process?

## Relationships
- How should the system prevent or handle cycles in parent–child links?
- What happens when a merge produces duplicate or contradictory relationships?
- What happens if relationship types conflict (biological versus adoptive) for the same endpoints?
- Are there any limits on multiple parents or multiple parent links of the same type?

## Claims and identity
- How is original-maintainer unavailability determined?
- How are competing claims to one profile handled?
- What is the retry/appeal policy after a claim rejection?
- How long is submitted evidence retained, and who can access it?

## Privacy and deletion
- Can a profile be permanently deleted, or only hidden behind a `Deleted` placeholder?
- Which audit metadata must remain if personal data is removed?
- Can relationship categories be shown when an endpoint is private?
- What is the privacy behavior for an account/profile that is abandoned?

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
