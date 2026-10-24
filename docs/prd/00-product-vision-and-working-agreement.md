# PRD 00 — Product Vision and Working Agreement

## Vision

Build an open-source, self-hostable web application for a globally interconnected, community-maintained family graph.

Priorities:
- Privacy and minimal personal data.
- Consent and clear authority over shared relationships.
- Traceable changes and reliable relationship history.
- Useful graph exploration without rendering the entire graph at once.

## Product principles

1. **People are not accounts.** A person may exist without an account.
2. **Minimal data.** Creating a person initially requires only a name.
3. **Direct facts are limited.** Users directly register parent–child relationships and marriages; other family relationships are derived.
4. **One maintainer per profile.** Each person profile has one responsible maintainer at a time.
5. **Consent is per relationship endpoint.** A claimed person can disagree with a relationship involving their profile.
6. **Privacy hides identity, not necessarily graph structure.** Public relationships may connect to `Private` or `Deleted` placeholders.
7. **History is retained.** Relationship changes, disputes, merges, and relevant decisions must be auditable.
8. **No silent assumptions.** Unresolved product rules must be documented and raised for clarification.

## Requirements-first working agreement for Claude Code

Before implementation:
- Read the relevant PRD files and summarize applicable requirements.
- Identify conflicts, ambiguities, and missing acceptance criteria.
- Ask concise questions when an unresolved decision blocks the task.
- Do not invent product rules or silently change these PRDs.
- Do not select a database, framework, infrastructure, or major dependency unless separately authorized.
- Propose domain concepts and invariants before proposing tables or code.
- Keep implementation changes scoped to the approved task.
- Add or update tests for the specified acceptance criteria when implementation is eventually authorized.

## Initial release boundaries

Included at product level:
- People and accounts as separate concepts.
- Parent–child relationships: biological or adoptive.
- Marriages between two people, current or former (PRD 02).
- Profile claims and manual claim appeals.
- Relationship consent, disagreement, reactivation, and audit history.
- Privacy settings and placeholders.
- Reviewed duplicate-profile merging.
- Derived relationship explanations and graph traversal.

Not required for initial release:
- A special workflow for deceased people; preserve their profiles as they were left.
- Guardianship, step-parent, foster-parent, or other relationship types beyond biological and adoptive parent–child links.
- A specific technology stack or deployment design.
