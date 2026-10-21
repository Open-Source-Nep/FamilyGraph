# PRD 08 — Derived Relationships and Graph Exploration

## Purpose

Enable people to explore a large interconnected family graph without treating derived relationships as independently asserted facts.

## Requirements

- Derive siblings, grandparents, cousins, and other supported family relationships from active parent–child links.
- Only active relationships participate in normal traversal and derivation.
- Relationship explanations should identify the path or links supporting the result.
- Preserve biological/adoptive distinctions along paths.
- Show an adoption indicator when a derived relationship's path crosses an adoptive parent–child link.
- Support search, graph traversal, relationship pathfinding, and explanations.
- Support graphs containing more than 10,000 people without rendering the entire graph simultaneously.
- Public traversal may pass through private/deleted placeholders where the underlying relationship is active, while hiding protected identity details.

## Acceptance criteria

- `AC-08.1` Derived relationships update when a supporting relationship becomes inactive or active again.
- `AC-08.2` Users can inspect an explanation for a derived relationship.
- `AC-08.3` Adoption is indicated when relevant.
- `AC-08.4` The application can display a local/subgraph view instead of rendering the entire dataset.

## Open details

- Cycle prevention and validation rules need finalization.
- How to present multiple valid paths between two people is undecided.
- Which derived relationship labels to support initially needs a product/UI decision.
- Unknown or undisclosed parents without a person profile are not yet specified.
