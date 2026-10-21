# PRD 01 — People and Profile Maintenance

## Purpose

Define person profiles, account separation, and profile-maintainer authority.

## Requirements

- A `Person` is a real-world individual represented in the graph. A person does not need a user account.
- A `UserAccount` is an authenticated application user. It may claim and maintain a person profile.
- A contributor may create multiple person profiles.
- Creating a person initially requires only a name.
- A person's name has a first name (required), a middle name (optional), and a last name (optional). No other profile data is required.
- Only the profile's current maintainer may rename the person.
- Each person profile has exactly one responsible maintainer at a time.
- Before a profile is claimed, its original contributor is its maintainer.
- After a claim is approved, the claimant becomes the profile's maintainer.
- Profile maintenance authority does not allow a maintainer to override another claimed person's relationship disagreement.
- A maintainer may manage profile information and initiate relationship changes subject to the relationship lifecycle rules.
- Separate profiles that represent the same real person are handled through the reviewed merge workflow. Do not silently combine them based on name similarity.
- Claiming a profile does not automatically deactivate its existing relationships.

## Account deletion and maintainer succession

- When an account is deleted, every profile it maintains becomes **maintained by administrators** as a group. Any current administrator may act as that profile's maintainer, and audit history records which administrator acted. This keeps exactly one maintainer per profile.
- A claimed person deleting their account chooses whether to delete their own profile (erasure, PRD 05) or leave it, in which case it is also maintained by administrators.
- Approvals and disagreements recorded before the deletion stay unchanged. Links the person disagreed with stay inactive.
- An administrator maintaining a profile has exactly a maintainer's powers. They cannot override another person's disagreement (PRD 03).
- **Succession:** any signed-in user may request to become the maintainer of a profile that is maintained by administrators, or whose maintainer is unavailable (as defined in PRD 04). An administrator decides the request.
- Succession never applies to a profile whose maintainer is the claimed person themselves while their account exists. Only a new identity claim (PRD 04) can change that maintainer.

## Acceptance criteria

- `AC-01.1` A person can exist without a corresponding account.
- `AC-01.2` One account can maintain multiple person profiles.
- `AC-01.3` A profile has one current maintainer, not multiple co-maintainers.
- `AC-01.4` Claim approval changes the maintainer to the claimant.
- `AC-01.5` Existing relationships remain in their current consent state after a successful claim.
- `AC-01.6` The application does not infer identity solely from matching names.
- `AC-01.7` A person can be created with only a first name; middle and last names are optional.
- `AC-01.8` Only the current maintainer can rename a person.
- `AC-01.9` Deleting an account makes its maintained profiles maintained by administrators, without changing any relationship's consent state.
- `AC-01.10` A claimed person deleting their account can choose to delete their own profile or leave it maintained by administrators.
- `AC-01.11` An administrator can approve a succession request for a profile maintained by administrators or whose maintainer is unavailable.

## Open details

- How account deletion is confirmed and how long it takes are implementation details.
