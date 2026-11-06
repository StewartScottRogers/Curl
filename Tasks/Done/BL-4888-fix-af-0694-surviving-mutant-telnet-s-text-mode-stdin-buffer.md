---
id: BL-4888
title: Fix AF-0694: Surviving mutant: telnet's text-mode stdin buffer can be two bytes short and overflow on a full read
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/WindowsTextModeReadStream.cs, Curl.Engine.UnitTests/Cgr112Tests.cs]
requirement: none
created: 2026-11-06
completed: 2026-11-06
---
# BL-4888 — Fix AF-0694: Surviving mutant: telnet's text-mode stdin buffer can be two bytes short and overflow on a full read

## Goal

The defect the audit office reported as AF-0694 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0694 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0694-surviving-mutant-telnet-s-text-mode-stdin-buffer-c.md`.

Location: `Curl.Engine.UnitLibrary/WindowsTextModeReadStream.cs:20`

Location: `Curl.Engine.UnitLibrary/WindowsTextModeReadStream.cs:20`

'private readonly byte[] decoded = new byte[RawReadSize + 1];' mutated to 'RawReadSize - 1' (4095) survived. Translate (line 100) writes up to one decoded byte per raw byte, plus a held CR, into decoded, and each raw read is up to RawReadSize (4096). So a telnet standard input that delivers 4095+ bytes in one read would throw IndexOutOfRangeException, changing the exit code and the bytes sent. No test reads more than a few bytes.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/WindowsTextModeReadStream.cs:20:+1 -Member decoded -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The production code was already right (`RawReadSize + 1`); the defect was a missing test. Added `Cgr112Tests.ReadAsync_FullRawReadAfterAHeldCarriageReturn_DecodesEveryByte`: 4095 bytes and a CR fill the first 4096-byte raw read (the CR held), then a full 4096-byte read decodes to the held CR plus 4096 bytes, 4097 in all, so both `RawReadSize - 1` and `RawReadSize` overflow and only `+ 1` passes.
- A lane may not run `Audit/Tools/Invoke-MutationTest.ps1`, so the mutant was applied by hand instead: with `RawReadSize - 1` the new test failed (1 failed, 10 passed in Cgr112Tests); restored, build clean and the fast tests green. No production code changed, so no quality run.
- The narrowing commit could not be pushed (push denied for the lane); it stays a local commit for the shift to integrate.

- Touches narrowed from the whole Curl.Engine.UnitLibrary to WindowsTextModeReadStream.cs and its tests in Curl.Engine.UnitTests/Cgr112Tests.cs, where the fix and its test land (ADR-0694).

## Log

- 2026-11-06: Created.
- 2026-11-06: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-06: Doing -> Done. A test with a held CR and a full 4096-byte raw read kills the decoded-buffer mutant
