# PRD 01 — People and Profile Maintenance

## Purpose

Define person profiles, account separation, and profile-maintainer authority.

## Requirements

- A `Person` is a real-world individual represented in the graph. A person does not need a user account.
- A `UserAccount` is an authenticated application user. It may claim and maintain a person profile.
- Creating a user account does not create a person profile.
- A contributor may create multiple person profiles.
- Creating a person initially requires only a name.
- On the Add a person page, the contributor may also choose the new person's parents and children (optional), from their own profiles, the people linked to them, or any Public profile found by name. Each becomes an ordinary link created after the profile, under the usual consent rules (PRD 03); a picked parent's spouses are offered as parents too (PRD 08).
- A person's name has a first name (required), a middle name (optional), and a last name (optional). No other profile data is required.
- Only the profile's current maintainer may rename the person.
- Each person profile has exactly one responsible maintainer at a time.
- Before a profile is claimed, its original contributor is its maintainer.
- After a claim is approved, the claimant becomes the profile's maintainer.
- Profile maintenance authority does not allow a maintainer to override another claimed person's relationship disagreement.
- A maintainer may manage profile information and initiate relationship changes subject to the relationship lifecycle rules.
- Separate profiles that represent the same real person are handled through the reviewed merge workflow. Do not silently combine them based on name similarity.
- Claiming a profile does not automatically deactivate its existing relationships.

## Gender

- A profile may have a gender: Male, Female or Other. It is optional and starts unset; a new person still needs only a first name.
- It can be given when the profile is created. Only the profile's maintainer may set, change or clear it. Changes are audited without the gender.
- The gender is shown wherever the name is, and hidden like the name (PRD 05). Relationship wording uses it (PRD 08).

## Profile image

- A profile may have one image (a photo). It is optional.
- Only the profile's maintainer may add, replace or remove it. Changes are audited without the image.
- The browser crops the image to a square, shrinks it and re-encodes it before upload, which drops embedded metadata such as the location where a photo was taken. The server accepts only JPEG, PNG or WebP images of at most 1 MiB.
- The image shows on the person page and in the graph page's diagram, in place of the initials. The home overview stays dots.

## Voluntary handover

- A maintainer may hand a profile over to another account. They create a one-time handover link and send it however they like; any signed-in user who opens it may accept it.
- A link expires after 14 days (configurable per instance). A profile has at most one open link: a new link replaces the previous one, and the maintainer may cancel it. A link stops working once it is accepted, cancelled or expired, or when its creator no longer maintains the profile.
- Accepting makes the receiver the profile's only maintainer. The previous maintainer keeps no authority over it.
- A handover never changes a relationship or its consent state. Approvals and disagreements already recorded stay as they are.
- A claimed profile cannot be handed over. Only an identity claim (PRD 04) changes its maintainer.
- Creating, cancelling and accepting a link are audited, without the link's secret.

## Administrator transfer

- An administrator may make another account the maintainer of a profile without that account accepting anything. It is the only exception to voluntary handover.
- An administrator may transfer one profile (from its person page) or every profile an account maintains at once (from the Users page). Deleted profiles are not transferred.
- An administrator has a Users page listing every account with its email, whether Google verified it, whether it is an administrator, and how many profiles it maintains. Opening an account lists those profiles, Private ones with their names, each with a link and its own transfer.
- A transfer never changes a relationship or its consent state, and each transferred profile is audited with the administrator, the previous and the new maintainer. An open handover link stops working because its creator no longer maintains the profile.
- A claimed profile cannot be transferred. Only an identity claim (PRD 04) changes its maintainer.
- Other users cannot see the Users page or transfer profiles.

## Account deletion and maintainer succession

- When an account is deleted, every profile it maintains is assigned to **one administrator account** as maintainer: the first address on the configured administrator list that belongs to an existing account. This keeps exactly one maintainer per profile.
- If that administrator's account is deleted or loses the administrator role, its inherited profiles move to the next administrator in the same way.
- A claimed person deleting their account chooses whether to delete their own profile (erasure, PRD 05) or leave it, in which case it is also assigned to an administrator.
- Approvals and disagreements recorded before the deletion stay unchanged. Links the person disagreed with stay inactive.
- An administrator maintaining an inherited profile has exactly a maintainer's powers. They cannot override another person's disagreement (PRD 03).
- **Succession:** any signed-in user may request to become the maintainer of a profile inherited by an administrator, or whose maintainer is unavailable (as defined in PRD 04). An administrator decides the request.
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
- `AC-01.9` Deleting an account assigns its maintained profiles to the first available administrator, without changing any relationship's consent state.
- `AC-01.10` A claimed person deleting their account can choose to delete their own profile or leave it assigned to an administrator.
- `AC-01.11` An administrator can approve a succession request for a profile inherited by an administrator or whose maintainer is unavailable.
- `AC-01.12` Registering or signing in to an account does not create a person profile.
- `AC-01.13` A maintainer can create a handover link for a profile they maintain; it expires after the configured period.
- `AC-01.14` A signed-in user who opens a valid handover link can accept it and becomes the profile's only maintainer.
- `AC-01.15` A handover leaves every relationship and its consent state unchanged.
- `AC-01.16` A handover link cannot be accepted once it has been used, cancelled, replaced or has expired, or when its creator no longer maintains the profile.
- `AC-01.17` A claimed profile cannot be handed over.
- `AC-01.18` A profile's maintainer can add, replace and remove its image; nobody else can.
- `AC-01.19` Only a JPEG, PNG or WebP image of at most 1 MiB is accepted.
- `AC-01.20` The Client shrinks and re-encodes an image before upload, removing its metadata.
- `AC-01.21` A person can be created with or without a gender, which is Male, Female or Other.
- `AC-01.22` Only a profile's maintainer can set, change or clear its gender, and the audit history records the change without the gender.
- `AC-01.23` An administrator can list every account with its email, verified flag, administrator flag and number of maintained profiles, and the profiles one account maintains.
- `AC-01.24` An administrator can make another account a profile's maintainer without that account's approval.
- `AC-01.25` An administrator can move every non-deleted profile one account maintains to another account at once.
- `AC-01.26` A transfer leaves every relationship and its consent state unchanged and is recorded in the audit history for each profile.
- `AC-01.27` A user who is not an administrator cannot list accounts or transfer profiles.
- `AC-01.28` A claimed profile cannot be transferred.
- `AC-01.29` Adding a person can optionally link chosen parents and children; each link follows the usual consent rules, and links that are not active are reported.

## Open details

- How account deletion is confirmed and how long it takes are implementation details.
