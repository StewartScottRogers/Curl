---
id: BL-4706
title: Fix AF-0558: Surviving mutant: a POP3 user name or password with a space can be refused with exit 43 without a test failing
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Pop3.UnitLibrary]
requirement: none
created: 2026-11-03
completed:
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

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
