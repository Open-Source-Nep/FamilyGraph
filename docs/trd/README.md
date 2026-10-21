# FamilyGraph — Technical Requirements Documents (TRD)

## Purpose

This folder is the technical handover for Claude Code. It records the current technical direction for FamilyGraph and identifies decisions that still require discussion.

**Do not implement the application yet.** Use these documents to continue the architecture discussion and update the TRDs as decisions are agreed.

## Source of truth

- Product behavior is defined by the project's separate, bite-sized PRD Markdown files.
- These TRDs define technical architecture and implementation conventions.
- Do not invent product rules to fill gaps in the PRDs. Record unresolved questions instead.
- Distinguish agreed decisions, recommendations, and unresolved decisions.

## Documents

- `architecture.md` — overall architecture and guiding principles.
- `technology-stack.md` — proposed frameworks, versions, and dependencies.
- `solution-structure.md` — repository layout and project responsibilities.
- `project-dependencies.md` — allowed project references and persistence boundaries.
- `open-decisions.md` — decisions to discuss before implementation.

## Collaboration instructions for Claude

1. Read the relevant PRDs before proposing technical behavior.
2. Work decision by decision; ask focused questions rather than making several major assumptions at once.
3. Explain meaningful trade-offs and challenge unnecessary complexity.
4. Do not write application code until the technical specification is sufficiently agreed.
5. Do not silently change settled decisions. If a technical contradiction requires revisiting one, explain why and ask first.
6. Keep these TRDs concise and avoid duplicating product requirements.
7. When a decision is agreed, update the relevant TRD and record significant architectural choices in `open-decisions.md` or a future ADR file.
