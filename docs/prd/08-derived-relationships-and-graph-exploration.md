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

## Exploration views

- The client offers a fan diagram and a tree diagram centred on one person.
- A view shows the centre person and a limited number of related people (about 10 by default, configurable per instance). People at the edge with further relatives show a "load more" control that expands the view around them.
- The related people are the ones nearest to the centre person in any direction, closest first: parents and children, then siblings, grandparents and grandchildren, and so on, within a limited number of generations above and below.
- Adoptive links are drawn differently from biological links (e.g. dashed) with a legend. The exact styling is a UI decision.
- Active marriages are a step in any direction, like parents and children: local views, the overview and paths walk them. Spouses are in the same generation, drawn side by side with a distinct line, and a former marriage is labelled as former.

## Relationship explanations

- The first release shows **no derived relationship labels** (such as "sibling" or "cousin"). An explanation shows the path between two people step by step, e.g. "Sita → parent: Maya → parent: Gita → child: Rina → child: You".
- Where the gender of the step's person is known and visible (PRD 01), a step reads "mother" or "father" instead of "parent", and "daughter" or "son" instead of "child". It stays "parent" or "child" when the gender is Other, not set, or hidden behind a placeholder. Lists of parents and children use the same words.
- A marriage step reads "husband" or "wife" where the spouse's gender is known and visible, "spouse" otherwise, with "former" before it once the marriage ended.
- An adoptive step reads "adoptive parent" or "adopted child" instead of "parent" or "child", or "adoptive mother", "adopted son" and so on where the gender is known.
- When two people are connected by more than one path, the shortest path is shown, with a note "There are more connections between these people" that lists up to a configurable number of further paths (default 5).

## Unknown or undisclosed parents

- There is no special state or placeholder for an unknown parent. A contributor represents one by creating an ordinary profile with the first name `UNKNOWN` and linking it under the normal rules.
- Profiles named `UNKNOWN` are never treated as the same person. Duplicate suggestions and merges must not match on this name (PRD 01, PRD 06).

## Planned: admin-defined relationship labels (not in the first release)

- Administrators define label rules for the instance. A rule maps an exact step sequence (each step "up" to a parent or "down" to a child) to a label, e.g. up, up, down, down → "first cousin".
- Labelling the relationship between two people uses the shortest path. No matching rule means no label, only the path.
- Labels are display text only and never become stored facts. Adoption on the path is still flagged. Rule changes are audited.
- A rule may also depend on the gender of the person being labelled (PRD 01), e.g. up, down with Female → "sister". When that gender is Other, not set or hidden behind a placeholder (PRD 05), the rule without a gender applies, e.g. up, down → "sibling". Gender rules never reveal a hidden gender.
- Rules cannot depend on birth order, which is stored per parent for display only (PRD 02). Distinguishing full and half siblings needs a built-in rule, because both share the step sequence up, down.
- Open: per-language labels, and whether to ship a default rule set.

## Acceptance criteria

- `AC-08.1` Derived relationships update when a supporting relationship becomes inactive or active again.
- `AC-08.2` Users can inspect an explanation for a derived relationship.
- `AC-08.3` Adoption is indicated when relevant.
- `AC-08.4` The application can display a local/subgraph view instead of rendering the entire dataset.
- `AC-08.5` A user can expand a view from an edge person with further relatives.
- `AC-08.6` An explanation between two people shows the shortest path and indicates when more paths exist, listing them on request.
- `AC-08.7` An explanation marks each adoptive step as "adoptive parent" or "adopted child".
- `AC-08.8` Where a person's gender is known and visible, they are named mother, father, daughter or son instead of parent or child; otherwise parent or child.
- `AC-08.9` Views and paths walk active marriages as one step within a generation; a spouse is named husband, wife or spouse, and "former" when the marriage ended.

## Open details

- Cycle prevention rules are decided in PRD 02. Traversal must still tolerate cycles defensively.
- Fan and tree diagram layout and styling are decided with the client graph UI.
