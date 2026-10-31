---
id: BL-4432
title: Fix AF-0510: TransferCredentialLookup.NetrcControlCodeFailure's HasControlCode(Login) || HasControlCode(Password) can become && with no test failing, so a netrc entry with a control code in only one of them is sent instead of refused with exit 26
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-10-31
completed:
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

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-31: Created.
