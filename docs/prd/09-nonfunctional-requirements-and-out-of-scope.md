# PRD 09 — Nonfunctional Requirements and Out of Scope

## Product qualities

- **Privacy:** Collect only necessary information; restrict access to sensitive claim evidence and non-public audit data.
- **Auditability:** Important state changes must be traceable.
- **Correctness:** Derived relationships must reflect only active direct relationships.
- **Scalability:** Support graphs larger than 10,000 people without rendering all nodes simultaneously.
- **Self-hostability:** The product must be open-source and capable of being deployed by third parties.
- **Accessibility:** Interactive graph exploration must not be the only way to inspect relationship information; provide readable relationship explanations and navigable alternatives.

## Data export

An administrator can download everything the instance stores as one zip file that a later import can read back. It holds every user account with its ID, Google login and email; every profile with its details, including birth dates hidden from others, and its photo (Private ones with their names; deleted ones as placeholders without any name or details); every relationship and marriage, deleted ones too, with each side's consent, the birth order and whether a marriage is former; the profile handover links (never the link itself); and the audit history. Everything that refers to a user refers to the account's ID. Relationship label rules ship with the app and are not exported. Other users cannot export. Each export is recorded in the audit history with the administrator and the time.

## Data import

An administrator can load an export file into an instance that has no profiles yet, for example to move to a new server or restore one. Everything comes back as it was in the file, with the same IDs: every profile, photo, relationship and marriage with its consent, visibility, gender, description, birth date and birth order, the handover links and the audit history. Each account in the file is created again with its ID, so its owner signs in with Google as before and finds their profiles; when an account here already has the same Google login (such as the administrator who imports), that account takes its place. Only exports in the current format can be imported; an older export is refused, and the old instance must be updated and export again. The instance sets the largest file it accepts. A file with any problem is refused whole and nothing is saved, and the administrator is shown the problems. Other users cannot import, and an instance that already has profiles refuses an import. The import itself is recorded in the audit history once, with the administrator and the time.

## Languages

The whole interface is offered in English and Nepali. Each instance chooses the language it starts in. Anyone may switch the language from the page header; the choice is remembered in that browser only and is not part of the account. Names, descriptions and other text people enter are shown as entered, never translated. In Nepali, numbers and dates use Devanagari digits, and the app brings its own Devanagari font so that text looks the same on every device and in print. Relationship words such as "adopted son" or "former wife" are translated as whole phrases, so each language can word them in its own order. Kinship terms that depend on culture (such as different words for a father's elder and younger brother) come from the chosen culture, not the language (PRD 08); the language switch offers only the current culture's languages.

## Appearance

The app has a clean, modern look: a light grey page with white rounded cards, one blue accent and tidy buttons and lists. It follows the device's light or dark setting, with no switch of its own; printed pages stay black on white. The header stays at the top with the main links and the culture and language switches; on a phone they fold behind a menu button so the graph keeps the whole width (agreed 2026-11-02).

## Initial release exclusions

- Dedicated deceased-person workflow. Preserve profiles as they were left.
- Parent relationship categories beyond biological and adoptive.
- Automatic merging or identity resolution based solely on names.
- Administrator override of required relationship consent. (An administrator may approve a pending side on behalf of its maintainer, PRD 03, but never against a decline or withdrawal.)
- Selection of a database, framework, hosting provider, or graph-storage technology in this PRD pack.

## Acceptance criteria

- `AC-09.1` The product remains usable when the graph is too large to render in full.
- `AC-09.2` Core relationship information can be understood through text explanations as well as graph visualization.
- `AC-09.3` Self-hosting is an explicit product goal, not tied to one hosting provider.
- `AC-09.4` An administrator can download one zip file with every account by its ID, every profile with its photo, every relationship and marriage with both sides' consent, the handover links and the audit history.
- `AC-09.5` A user who is not an administrator cannot export data.
- `AC-09.6` The export includes Private profiles with their names, deleted profiles without any name, and deleted relationships and marriages, and refers to users only by account ID.
- `AC-09.7` Each export is recorded in the audit history with the administrator and the time.
- `AC-09.8` An administrator can import an export file into an instance without profiles, and every profile, photo, relationship, marriage, handover link and audit entry comes back with its ID, consent, visibility, gender, description, birth date and birth order.
- `AC-09.9` A user who is not an administrator cannot import, and an instance that has any profile refuses an import.
- `AC-09.10` Each exported account is created again with its ID and Google login, so an imported profile keeps its maintainer; an account here with the same Google login takes that account's place.
- `AC-09.11` A file with any problem is refused whole, nothing is saved, and the problems are listed.
- `AC-09.12` Each import is recorded once in the audit history with the administrator and the time.
- `AC-09.13` Every screen of one build can be shown in English or Nepali, and a message without a translation falls back to English.
- `AC-09.14` The instance configuration sets the language the app starts in for anyone who has not chosen one.
- `AC-09.15` Anyone can switch the language from the page header; the choice is remembered in that browser only, not in the account.
- `AC-09.16` In Nepali, numbers and dates use Devanagari digits on screen and in print, with a bundled Devanagari font.
- `AC-09.17` Relationship words (such as "adopted son" or "former wife") are translated as whole phrases.
- `AC-09.18` Every page follows the device's light or dark setting, and printed pages are black on white.
- `AC-09.19` On a narrow screen the header's links and switches fold behind a menu button.
