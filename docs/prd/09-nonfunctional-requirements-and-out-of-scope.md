# PRD 09 — Nonfunctional Requirements and Out of Scope

## Product qualities

- **Privacy:** Collect only necessary information; restrict access to sensitive claim evidence and non-public audit data.
- **Auditability:** Important state changes must be traceable.
- **Correctness:** Derived relationships must reflect only active direct relationships.
- **Scalability:** Support graphs larger than 10,000 people without rendering all nodes simultaneously.
- **Self-hostability:** The product must be open-source and capable of being deployed by third parties.
- **Accessibility:** Interactive graph exploration must not be the only way to inspect relationship information; provide readable relationship explanations and navigable alternatives.

## Data export

An administrator can download the whole instance as one file that a later import can read back. It holds every profile with its details, including birth dates hidden from others (Private ones with their names; deleted ones as placeholders without any name or details), every relationship that has not been deleted with each side's consent and the birth order, every marriage that has not been deleted with each side's consent and whether it is former, and the email of each account that maintains a profile. It holds no profile images, audit history or deleted relationships. Other users cannot export. Each export is recorded in the audit history with the administrator and the time.

## Data import

An administrator can load an export file into an instance that has no profiles yet, for example to move to a new server or restore one. Every profile, relationship and marriage comes back as it was in the file, with its consent, visibility, gender, description, birth date and birth order. Each profile's maintainer is the account here with the same verified email; when there is no such account, the administrator who imports becomes its maintainer. A file with any problem is refused whole and nothing is saved, and the administrator is shown the problems. Other users cannot import, and an instance that already has profiles refuses an import. Each import is recorded in the audit history once, with the administrator and the time; the imported rows get no history entries of their own.

## Initial release exclusions

- Dedicated deceased-person workflow. Preserve profiles as they were left.
- Parent relationship categories beyond biological and adoptive.
- Automatic merging or identity resolution based solely on names.
- Administrator override of required relationship consent.
- Selection of a database, framework, hosting provider, or graph-storage technology in this PRD pack.

## Acceptance criteria

- `AC-09.1` The product remains usable when the graph is too large to render in full.
- `AC-09.2` Core relationship information can be understood through text explanations as well as graph visualization.
- `AC-09.3` Self-hosting is an explicit product goal, not tied to one hosting provider.
- `AC-09.4` An administrator can download one file with every profile, every relationship that has not been deleted with both sides' consent, and the email of each profile's maintainer.
- `AC-09.5` A user who is not an administrator cannot export data.
- `AC-09.6` The export includes Private profiles with their names and deleted profiles without any name, and leaves out images, audit history and deleted relationships.
- `AC-09.7` Each export is recorded in the audit history with the administrator and the time.
- `AC-09.8` An administrator can import an export file into an instance without profiles, and every profile, relationship and marriage comes back with its consent, visibility, gender, description, birth date and birth order.
- `AC-09.9` A user who is not an administrator cannot import, and an instance that has any profile refuses an import.
- `AC-09.10` An imported profile is maintained by the account with its maintainer's verified email, or by the importing administrator when there is none.
- `AC-09.11` A file with any problem is refused whole, nothing is saved, and the problems are listed.
- `AC-09.12` Each import is recorded once in the audit history with the administrator and the time.
