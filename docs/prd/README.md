# Community-Driven Family Graph — PRD Pack

This folder contains bite-sized product requirements documents (PRDs) for Claude Code. They define product behavior before technology and implementation choices.

## How to use with Claude Code

1. Copy this folder into the project repository, for example under `docs/prd/`.
2. Start with `00-product-vision-and-working-agreement.md`.
3. Ask Claude Code to review one PRD at a time and identify ambiguities before implementing anything.
4. Keep decisions in these PRDs as the source of truth. Do not silently invent answers for unresolved items.

## Suggested order

1. `00-product-vision-and-working-agreement.md`
2. `01-people-and-profile-maintenance.md`
3. `02-parent-child-relationships.md`
4. `03-relationship-consent-and-lifecycle.md`
5. `04-profile-claims-and-identity-verification.md`
6. `05-privacy-and-removal.md`
7. `06-duplicate-detection-and-merging.md`
8. `07-disputes-and-audit-history.md`
9. `08-derived-relationships-and-graph-exploration.md`
10. `09-nonfunctional-requirements-and-out-of-scope.md`
11. `10-open-decisions.md`

## Scope note

These documents are a product-level specification, not a database schema, architecture decision record, or implementation plan. Entity names are illustrative. Do not choose databases, frameworks, hosting, or infrastructure until the product rules are sufficiently stable.
