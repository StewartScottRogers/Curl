---
id: BL-4706
title: Fix AF-0558: Surviving mutant: a POP3 user name or password with a space can be refused with exit 43 without a test failing
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Pop3.UnitLibrary, Curl.Original.Protocol.Pop3.UnitTests/Pop3ProtocolHandlerAdversarialTests.cs]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4706 — Fix AF-0558: Surviving mutant: a POP3 user name or password with a space can be refused with exit 43 without a test failing

## Goal

The defect the audit office reported as AF-0558 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0558 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0558-surviving-mutant-a-pop3-user-name-or-password-with.md`.

Location: `Curl.Original.Protocol.Pop3.UnitLibrary/Pop3ProtocolHandler.cs:87`

Location: `Curl.Original.Protocol.Pop3.UnitLibrary/Pop3ProtocolHandler.cs:87`

Mutant 'character < ' '' -> 'character <= ' '' in HasControlByte survived. TransferAsync refuses credentials for which it holds with CurlExitCode.BadFunctionArgument before connecting, so the mutant makes a password such as 'a b' fail with exit 43 and send nothing. No test pins a credential holding a space.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Original.Protocol.Pop3.UnitLibrary/Pop3ProtocolHandler.cs:87:< -Member HasControlByte -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added `ExecuteAsync_CredentialsWithSpace_LogsInWithTheSpaceSentVerbatim` (user name and password each holding a space log in with USER/PASS sent verbatim, exit 0) to `Pop3ProtocolHandlerAdversarialTests.cs`; touches widened to that test file, which no task in Doing holds. No production change, so no quality run.
- A lane may not run `Audit/Tools/Invoke-MutationTest.ps1`, so the reproduction was checked by hand: with `character <= ' '` applied to `HasControlByte`, both new cases fail (mutant killed); with the code restored they pass. The re-audit confirms it.
- `dotnet build` clean (0 warnings) and the fast tests green on Windows; the test is platform-neutral (fake connection, no paths).

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-03: Doing -> Done. A POP3 user name or password with a space is pinned to log in verbatim, killing the HasControlByte <= mutant
