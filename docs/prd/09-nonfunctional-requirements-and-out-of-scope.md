# PRD 09 — Nonfunctional Requirements and Out of Scope

## Product qualities

- **Privacy:** Collect only necessary information; restrict access to sensitive claim evidence and non-public audit data.
- **Auditability:** Important state changes must be traceable.
- **Correctness:** Derived relationships must reflect only active direct relationships.
- **Scalability:** Support graphs larger than 10,000 people without rendering all nodes simultaneously.
- **Self-hostability:** The product must be open-source and capable of being deployed by third parties.
- **Accessibility:** Interactive graph exploration must not be the only way to inspect relationship information; provide readable relationship explanations and navigable alternatives.

## Initial release exclusions

- Dedicated deceased-person workflow. Preserve profiles as they were left.
- Parent relationship categories beyond biological and adoptive.
- Automatic merging or identity resolution based solely on names.
- Administrator override of required relationship consent.
- Selection of a database, framework, hosting provider, or graph-storage technology in this PRD pack.

## Acceptance criteria

- The product remains usable when the graph is too large to render in full.
- Core relationship information can be understood through text explanations as well as graph visualization.
- Self-hosting is an explicit product goal, not tied to one hosting provider.
