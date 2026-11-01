---
id: BL-4432
title: Fix AF-0510: TransferCredentialLookup.NetrcControlCodeFailure's HasControlCode(Login) || HasControlCode(Password) can become && with no test failing, so a netrc entry with a control code in only one of them is sent instead of refused with exit 26
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests/EasyAuthenticationOptionsReadingTests.cs]
requirement: none
created: 2026-10-31
completed: 2026-10-31
---
# BL-4432 — Fix AF-0510: TransferCredentialLookup.NetrcControlCodeFailure's HasControlCode(Login) || HasControlCode(Password) can become && with no test failing, so a netrc entry with a control code in only one of them is sent instead of refused with exit 26

## Goal

The defect the audit office reported as AF-0510 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0510 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0510-transfercredentiallookup-netrccontrolcodefailure-s.md`.

Location: `Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:230`

Location: `Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:230`

Invoke-MutationTest.ps1 -Library Curl.Engine.UnitLibrary -MaxMutants 40 -Seed 0 reported 'survived Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:230 ||' (member NetrcControlCodeFailure): '&& (HasControlCode(result.Login) || HasControlCode(result.Password))' mutated to '&& (HasControlCode(result.Login) && HasControlCode(result.Password))' and every Curl.Engine.UnitTests test passed. The method is curl's exit-26 refusal (lib/url.c str_has_ctrl, BL-1411) of a matching netrc entry whose login or password holds a byte below 0x20, for every scheme except http, https, ws and wss. Under the mutant, an entry with a control code in only the login, or only the password, is no longer refused: the credentials are sent and the transfer goes on instead of exiting 26. No test has a netrc entry with a control code in exactly one of the two fields. This changes an exit code and request bytes.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:230:|| -Member NetrcControlCodeFailure -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed: a test with a control code in only the netrc login (or only the password) fails under the mutant
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- touches narrowed from Curl.Engine.UnitLibrary to the one test file the fix lands in: the production code is right, only a test was missing; no task in Doing on origin/work/dark-factory holds that file. Not pushed separately: the lane prompt forbids a lane pushing, so the shift integrates it with the fix.
- Fix: TryLookUp_NetrcEntryWithAControlCodeInOneFieldOnFtp_FailsWithExit26, two rows (0x01 in the login only, in the password only) on ftp://, each expecting exit 26 and the control code message, so the || to && mutant fails.
- Reproduction: the finding's mutation script is closed to lanes, so the mutant was applied by hand (the || at TransferCredentialLookup.cs:230 made &&): both new rows failed under it and passed once it was reverted, so the mutant is killed. Production code is unchanged, so no Measure-CodeQuality run was needed. dotnet build clean; fast tests green (exit 0). Platform-neutral: no paths, OS text or certificates.

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-31: Doing -> Done. A netrc entry with a control code in only its login or only its password is pinned to exit 26 on ftp, killing AF-0510's mutant
