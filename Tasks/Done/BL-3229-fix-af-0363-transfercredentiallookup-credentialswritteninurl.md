---
id: BL-3229
title: Fix AF-0363: TransferCredentialLookup.CredentialsWrittenInUrl's && can become || with no test failing, so http://zz@host/ sends no credentials
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/TransferCredentialLookup.cs, Curl.Engine.UnitTests/EasyAuthenticationOptionsReadingTests.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3229 — Fix AF-0363: TransferCredentialLookup.CredentialsWrittenInUrl's && can become || with no test failing, so http://zz@host/ sends no credentials

## Goal

The defect the audit office reported as AF-0363 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0363 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0363-transfercredentiallookup-credentialswritteninurl-s.md`.

Location: `Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:284`

Location: `Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:284`

Invoke-MutationTest -Library Curl.Engine.UnitLibrary -MaxMutants 40 -Seed 0: survived. Original: return user is null && password is null ? null : new NetworkCredential(user ?? string.Empty, password ?? string.Empty); mutated: user is null || password is null. The doc comment and the Curl.Engine CLAUDE.md pin curl 8.21.0 sending `zz:` for http://zz@host/ and `:x` for http://:x@host/. Under the mutant a URL with only a user name or only a password yields null credentials, so no Authorization header is sent: request bytes change and no Curl.Engine.UnitTests test notices.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:284:&& -Member CredentialsWrittenInUrl -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added `Curl.Engine.UnitTests/EasyAuthenticationOptionsReadingTests.cs` to touches: the fix is a test (the production code was right; only the test was missing). No other task in Doing on origin/work/dark-factory names it.
- Added `TryLookUp_NoNetrcAndUrlGivesOnlyOneHalf_SendsItWithTheOtherHalfEmpty` (`http://zz@h/` sends `zz:`, `http://:x@h/` sends `:x`) and `TryLookUp_NoNetrcAndUrlWithEmptyUserInformation_SendsNoCredentials`.
- A lane may not run `Audit/Tools`, so the reproduction was checked by hand: with the `&&` changed to `||` in `CredentialsWrittenInUrl`, both DataRows of the new test fail (mutant killed), and they pass once it is put back. The tests are pure computation with no paths, so they are platform-neutral. The build is clean and the fast tests are green.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-23: Doing -> Done. Unit tests pin URL credentials with only a user or only a password, killing the && to || mutant
