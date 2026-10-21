# PRD 01 — People and Profile Maintenance

## Purpose

Define person profiles, account separation, and profile-maintainer authority.

## Requirements

- A `Person` is a real-world individual represented in the graph. A person does not need a user account.
- A `UserAccount` is an authenticated application user. It may claim and maintain a person profile.
- A contributor may create multiple person profiles.
- Creating a person initially requires only a name.
- Each person profile has exactly one responsible maintainer at a time.
- Before a profile is claimed, its original contributor is its maintainer.
- After a claim is approved, the claimant becomes the profile's maintainer.
- Profile maintenance authority does not allow a maintainer to override another claimed person's relationship disagreement.
- A maintainer may manage profile information and initiate relationship changes subject to the relationship lifecycle rules.
- Separate profiles that represent the same real person are handled through the reviewed merge workflow. Do not silently combine them based on name similarity.
- Claiming a profile does not automatically deactivate its existing relationships.

## Acceptance criteria

- `AC-01.1` A person can exist without a corresponding account.
- `AC-01.2` One account can maintain multiple person profiles.
- `AC-01.3` A profile has one current maintainer, not multiple co-maintainers.
- `AC-01.4` Claim approval changes the maintainer to the claimant.
- `AC-01.5` Existing relationships remain in their current consent state after a successful claim.
- `AC-01.6` The application does not infer identity solely from matching names.

## Open details

- Account deletion and maintainer succession are not yet specified.
- Rules for a maintainer becoming unavailable are defined only for claim review in PRD 04; other cases need clarification.
