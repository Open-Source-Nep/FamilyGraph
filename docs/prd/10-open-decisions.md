# PRD 10 — Open Decisions Register

This file is intentionally unresolved. Claude Code must not silently invent decisions listed here. Update each item when the product owner decides it.

## People and accounts
- **Decided (2026-10-21):** profiles of a deleted account are assigned to one administrator account (the first on the configured list with an existing account; they move on if that administrator is removed). A claimed person may instead delete their own profile. Recorded consent is unchanged. See PRD 01.
- **Decided (2026-10-23):** a maintainer may hand a profile over voluntarily with a one-time link (14 days, configurable) that any signed-in user may accept; the receiver becomes the only maintainer and consent is unchanged. Claimed profiles cannot be handed over. Rejected alternatives: choosing the receiver by the email of an existing account (reveals who has an account); letting a claimed person give their profile away and unclaim it. See PRD 01.
- **Decided (2026-10-23):** a profile may have one image, set only by its maintainer and stored in the database. The browser shrinks and re-encodes it (dropping photo metadata such as location); the server accepts JPEG, PNG or WebP up to 1 MiB. It shows on the person page and graph page avatars, not on the home dots. Placeholders never show it, and deletion erases it. Rejected alternatives: re-encoding on the server (needs a new image library); storing uploads unchanged (keeps location metadata). See PRD 01.
- **Decided (2026-10-25):** a profile may have an optional one-line description (at most 200 characters), private like the name, and an optional birth date: a year, a year and month, or a full date, stored as entered in BS or AD. The birth date has a "show to others" switch, off by default; until it is on, only the maintainer and administrators see it. Only the maintainer edits them, audited without the values. Rejected alternatives: a longer bio; full dates only; always storing BS or always AD; showing birth dates like the name; never showing them to others. See PRD 01 and PRD 05.
- **Decided (2026-10-21):** an administrator may grant a succession request for a profile inherited by an administrator or whose maintainer is unavailable. A claimed person's own profile changes maintainer only through a claim.

## Relationships
- **Decided (initial invariant, 2026-10-21):** a person cannot be their own parent. A direct two-person cycle (A parent of B and B parent of A) is rejected.
- **Decided (2026-10-21):** deeper cycles (A → B → C → A) are rejected when a link is created or reactivated, if the proposed child is already an ancestor of the proposed parent. The check counts active, pending, and inactive links (not deleted ones). If the bounded check hits its limit, the link is rejected with an explanation that it could not be verified as cycle-free. Rejected alternatives: allow and flag for review; defer to the database/traversal decisions. See PRD 02.
- **Decided (2026-10-21):** duplicate or contradictory relationships from a merge are resolved by the reviewers choosing one link to keep. See PRD 06.
- **Decided (2026-10-21):** at most one parent–child link per ordered pair (parent, child), so the same pair cannot have both a biological and an adoptive link.
- **Decided (2026-10-21):** no limit on the number of parents, of either type.
- **Decided (2026-10-22):** a parent's children have a birth order for display only, set manually per parent by the parent's maintainer, with no consent and no birth dates. New children are unordered until placed. Rejected alternatives: an optional birth date on Person; letting the child's maintainer reorder; child-side approval of order changes. See PRD 02.
- **Decided (2026-10-25):** birth order stays per parent on each link, now set with up and down arrows by the parent's maintainer; a person-level sibling order was rejected. Children without a manual order sort by birth dates shown to others, then by name; hidden dates never affect the order. This replaces "no birth dates" above. See PRD 02.
- **Decided (2026-10-23, 2026-10-24):** marriages are a second direct relationship. Consent works as for parent–child links; an ended marriage is marked former and stays in the graph, marked or undone by either spouse's maintainer without consent; any number per person, one per pair; any genders; a marriage between a direct parent and child, or a parent–child link between spouses, is rejected (direct links only). See PRD 02.

## Claims and identity
- **Decided (2026-10-21):** a maintainer is unavailable after 30 days without response (configurable) or when their account no longer exists. See PRD 04.
- **Decided (2026-10-21):** parallel claims are allowed; with more than one open claim, an administrator decides them together.
- **Decided (2026-10-21):** one appeal per claim; after an administrator rejection, a new claim is allowed after 90 days (configurable) with new evidence. Claims on already-claimed profiles go to an administrator, and the current maintainer is notified.
- **Decided (2026-10-23):** a maintainer may mark a profile they maintain as themselves without approval, recorded as a self-approved identity claim. An account can have at most one claimed profile, and no second claim while it has one or an open claim. The form of claim evidence is still open. To be written into PRD 04 with #25.
- **Decided (2026-10-21):** evidence is visible to the claimant, reviewing administrators and the deciding maintainer, and is deleted 90 days after the final decision (configurable). Audit keeps the decision and rationale.

## Privacy and deletion
- **Decided (2026-10-21):** deletion permanently erases personal data and leaves a `Deleted` placeholder. It is not reversible. The maintainer or an administrator may delete. No new links to a deleted profile. See PRD 05.
- **Decided (2026-10-21):** audit history keeps event types, timestamps, account, profile and relationship IDs. Erased personal data is replaced with `[erased]`.
- **Decided (2026-10-21):** a relationship's biological or adoptive type may be shown next to a placeholder.
- **Decided (2026-10-21):** abandonment changes nothing automatically.
- **Decided (2026-10-21):** profiles are Public (default) or Private. The graph is visible only to signed-in users. Maintainers and administrators see through `Private`.

## Merging
- **Decided (2026-10-21):** anyone may propose a merge; each profile's maintainer approves. An administrator may stand in only for an unavailable maintainer of an unclaimed profile. Two claimed profiles are never merged. See PRD 06.
- **Decided (2026-10-21):** moved links keep their approvals; reviewers choose which of two clashing links to keep; merges that would create a self-link or cycle are blocked.
- **Decided (2026-10-21):** merges are not reversible.

## Audit and moderation
- **Decided (2026-10-21):** relationship audit history is visible to the current maintainers of both profiles and administrators. See PRD 07.
- **Decided (2026-10-21):** audit records are kept indefinitely; personal data in them is erased on profile deletion.
- **Decided (2026-10-21):** signed-in users can report abuse to an administrator queue. Administrators can suspend accounts; suspension blocks sign-in and changes but leaves profiles, relationships and consent untouched.

## Graph exploration
- **Decided (2026-10-21):** show the shortest path, with "There are more connections between these people" listing up to 5 more (configurable). See PRD 08.
- **Decided (2026-10-21):** no derived relationship labels in the first release; explanations show the path. Admin-defined label rules are planned for later. (2026-10-24: those rules may depend on the labelled person's gender, with a neutral fallback when it is not known.)
- **Decided (2026-10-21):** unknown parents are ordinary profiles with the first name `UNKNOWN`; no special state.
- **Decided (2026-10-22):** a local view shows the people nearest to the centre in any direction, closest first, rather than only ancestors and descendants. See PRD 08.
- **Decided (2026-10-23):** active marriages are a full step in views, the overview and paths; spouses share a generation, with a distinct line, and former marriages are labelled. See PRD 08.
- **Decided (2026-10-21):** adoptive steps read "adoptive parent"/"adopted child"; adoptive links are drawn differently with a legend.
- **Decided (2026-10-24):** relatives can be added from the graph page's diagram by right-clicking a person: Add parent, Add children or Add spouse. The list starts with your connections, and typing searches every Public profile so that separate families can be joined; on someone else's person it offers only your own profiles. Picking works as on the person page, with "Add a new person". Rejected alternatives: only your connections (two families could never be joined); search only; the menu also on the home dots or in the text view; the menu only on your own profiles; one person per action. See PRD 08.
- **Decided (2026-10-24):** a profile can be deleted from the graph page's right-click menu, by its maintainer or an administrator, under the existing deletion rules (PRD 05), after a confirmation dialog that names the person and says it cannot be undone. Rejected alternatives: typing the name to confirm; confirming inside the menu. See PRD 08.
- **Decided (2026-10-25):** adding children or parents offers to link the spouses too: each spouse through an active marriage has a checkbox, ticked when current and unticked when former, and the extra links copy the chosen type. The Add a person page can also choose the new person's parents and children. Rejected alternatives: current spouses only; every spouse ticked; a separate type per spouse; the parent and child pickers also inside the "Add a new person" dialogs. See PRD 01 and PRD 08.
- **Decided (2026-10-25):** one people picker everywhere people are picked, including the person page's spouse form, each row with photo or initials, name and description, and each with "Add a new person". The People page shows the same list rows. On the person page the picker follows the graph's rules, so a person you maintain can be linked to any Public profile. Rejected alternatives: cards in a grid on the People page; keeping the person page's own lists of your profiles. See PRD 01 and PRD 08.

## Administrator transfer
- **Decided (2026-10-25):** an administrator can move one profile, or every profile an account maintains, to another account without that account's approval, from an admin-only Users page that shows each account's email, verified and administrator flags, number of maintained profiles and an expandable list of those profiles. Consent is unchanged and each profile's transfer is audited. Rejected alternatives: only single profiles; only whole accounts; showing sign-up and last sign-in times (new stored personal data). See PRD 01.

## Data export
- **Decided (2026-10-23):** only administrators export, and the export covers the whole instance: every profile (Private ones with names, deleted ones without), every non-deleted relationship with its consent state and birth order, and the maintainers' emails, as one file. Images, audit history and deleted relationships are left out. Each export is audited. Rejected alternatives: per-user export of one's own profiles; both; exporting images; only the active graph. See PRD 09.

## Data import
- **Decided (2026-10-24):** an administrator imports an export file only into an instance without profiles. Each profile goes to the account with its maintainer's email; when no such account exists, the importing administrator becomes the maintainer. A file with any problem is refused whole, with the problems listed. The import is recorded as one audit entry. Rejected alternatives: importing into an instance with data, skipping existing IDs or refusing only on clashes; the first configured administrator inheriting unmatched profiles; an audit entry per imported row; importing the valid rows and skipping the rest. See PRD 09.

## General rule
When work depends on an unresolved item:
1. Explain why the decision matters.
2. Offer a small set of options with consequences.
3. Ask the product owner before implementing behavior that depends on it.
