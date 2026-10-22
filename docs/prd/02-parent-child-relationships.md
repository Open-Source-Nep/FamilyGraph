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

## Cycle prevention

- A person cannot be their own parent, and a direct two-person cycle (A parent of B and B parent of A) is rejected.
- A link is rejected when it is created or reactivated if the proposed child is already an ancestor of the proposed parent (for example A → B → C, then C → A).
- The ancestor check counts all non-deleted links: active, pending, and inactive. A pending link cannot later be approved into a cycle, and an inactive link cannot be reactivated into one.
- The ancestor check is bounded. If it reaches its limit before it can rule out a cycle, the link is rejected with an explanation that it could not be verified as cycle-free. The concrete limits are a technical decision.
- Graph traversal must still tolerate cycles defensively.

## Birth order of children (agreed 2026-10-22)

- A parent's children have a birth order, eldest first. It sorts that parent's children in views and path explanations only. It is not used to derive relationships or labels (PRD 08).
- The order is set manually per parent. No birth dates are stored. Each parent has their own order, so half siblings are ordered within each parent's list.
- Only the parent profile's maintainer changes the order. It needs no consent and never changes a link's consent state or whether it is active. Every change is audited (PRD 07).
- A newly linked child is unordered until the parent's maintainer places it. Views list ordered children first, then unordered ones by name.
- Pending and inactive links keep their position, but only active links are shown.

## Acceptance criteria

- `AC-02.1` A user can create biological and adoptive parent–child relationships.
- `AC-02.2` The system can derive a sibling relationship from shared active parent links.
- `AC-02.3` The system can distinguish biological and adoptive paths when explaining a derived relationship.
- `AC-02.4` The system indicates when an adoptive connection is relevant to a derived relationship.
- `AC-02.5` Inactive/disagreed parent–child links are excluded from normal graph traversal and derived relationships.
- `AC-02.6` Creating or reactivating a link is rejected when the proposed child is already an ancestor of the proposed parent through active, pending, or inactive links.
- `AC-02.7` When the bounded ancestor check cannot rule out a cycle, the link is rejected with an explanation that it could not be verified as cycle-free.
- `AC-02.8` A parent's maintainer can place each of that parent's children in birth order, or make a child unordered again.
- `AC-02.9` Only the parent's maintainer can change the order, and changing it never changes consent or link activity.
- `AC-02.10` A newly linked child is unordered until placed.

## Open details

- The exact visual icon and wording for adoption are UI decisions.
- Detailed treatment of unusual or conflicting family structures is covered by PRDs 08 and 10.
