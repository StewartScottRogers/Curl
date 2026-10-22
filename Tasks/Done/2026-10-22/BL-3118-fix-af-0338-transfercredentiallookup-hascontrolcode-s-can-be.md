---
id: BL-3118
title: Fix AF-0338: TransferCredentialLookup.HasControlCode's && can become || with no test failing, so every non-null URL credential on a non-http scheme is refused
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests/EasyAuthenticationOptionsReadingTests.cs]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3118 — Fix AF-0338: TransferCredentialLookup.HasControlCode's && can become || with no test failing, so every non-null URL credential on a non-http scheme is refused

## Goal

The defect the audit office reported as AF-0338 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0338 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0338-transfercredentiallookup-hascontrolcode-s-can-beco.md`.

Location: `Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:238`

Location: `Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:238`

Sampled mutant (seed 0) survived: 'text is not null && text.AsSpan().IndexOfAnyInRange(...) >= 0' became 'text is not null || ...'. HasControlCode feeds HasRefusedControlCode, which line 168 uses on the URL's decoded user name and password for every scheme but http, https, ws and wss. With the mutant, any user name or password in such a URL (ftp://user:pw@host/) counts as holding a control code, so the transfer fails with curl: (3) error extracting credentials from URL before connecting. That is a user-visible exit code, and no Curl.Engine.UnitTests test noticed.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:238:&& -Member HasControlCode -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added `TryCheckUrlCredentials_PlainUrlCredentialsOnFtp_Passes` (`ftp://user:pw@h/` passes with no failure) to `EasyAuthenticationOptionsReadingTests`; no production change. Added that test file to `touches` (no other Doing task names it).
- A lane may not run `Audit/Tools/Invoke-MutationTest.ps1`, so the mutant was applied by hand (`&&` to `||` at line 238): the new test failed (1 of 12), so the mutant is killed; reverted. Build clean, fast tests green (44 projects passed). No production code changed, so no Measure-CodeQuality.ps1 run.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-22: Doing -> Done. A unit test now kills the HasControlCode && to || mutant: plain ftp URL credentials must pass
