# PRD 02 — Parent–Child Relationships

## Purpose

Define the only relationship type users may register directly in the initial release.

## Supported types

- **Biological:** a biological parent–child relationship.
- **Adoptive:** an adoptive parent–child relationship.

Do not add guardianship, step-parent, foster-parent, or other parent categories in the initial release without a product decision.

## Requirements

- Users directly register parent–child relationships only.
- Siblings, grandparents, cousins, and other family relationships are derived from the active parent–child graph.
- A person may have multiple parent relationships.
- Adoption is metadata on the parent–child relationship, not a separate person or account type.
- Biological and adoptive connections must remain distinguishable in graph traversal and relationship explanations.
- If a derived relationship's path crosses an adoptive parent–child connection, show an adoption indicator in the derived relationship explanation or visualization.
- Relationship state and consent rules are specified in PRD 03.
- Profile privacy does not by itself erase a relationship from the graph. See PRD 05.

## Acceptance criteria

- A user can create biological and adoptive parent–child relationships.
- The system can derive a sibling relationship from shared active parent links.
- The system can distinguish biological and adoptive paths when explaining a derived relationship.
- The system indicates when an adoptive connection is relevant to a derived relationship.
- Inactive/disagreed parent–child links are excluded from normal graph traversal and derived relationships.

## Open details

- The exact visual icon and wording for adoption are UI decisions.
- Detailed treatment of unusual or conflicting family structures is covered by PRDs 08 and 10.
