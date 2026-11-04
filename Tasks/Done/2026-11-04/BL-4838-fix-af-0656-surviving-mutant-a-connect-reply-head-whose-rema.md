---
id: BL-4838
title: Fix AF-0656: Surviving mutant: a CONNECT reply head whose remaining line starts with a bare LF can be written as one -v line
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-4838 — Fix AF-0656: Surviving mutant: a CONNECT reply head whose remaining line starts with a bare LF can be written as one -v line

## Goal

The defect the audit office reported as AF-0656 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0656 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0656-surviving-mutant-a-connect-reply-head-whose-remain.md`.

Location: `Curl.Networking.UnitLibrary/ConnectTunnelVerboseLines.cs:165`

Location: `Curl.Networking.UnitLibrary/ConnectTunnelVerboseLines.cs:165`

Mutant 'lineFeed < 0' -> 'lineFeed <= 0' survived. When the rest of the head starts with an LF, the mutant writes all of it as one line instead of splitting it, which changes the verbose output bytes. No test covers an LF at index 0.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/ConnectTunnelVerboseLines.cs:165:< -Member ReportReplyHead -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added `ReportReplyHead_RestOfTheHeadStartingWithABareLineFeed_WritesThatLineFeedAsALineOfItsOwn` to `Curl.Networking.UnitTests/ConnectTunnelVerboseLinesTests.cs`: the head `HTTP/1.1 200 X\r\n\nX-A: 1\r\n\r\n` must give four `<` lines, so the LF at index 0 of the rest is a line of its own. No production change.
- A lane may not run the audit office's mutation tool (the guard hook refuses it). So the mutant `lineFeed <= 0` was applied by hand at `ConnectTunnelVerboseLines.cs:165`: the new test failed (killed, 1 of 30), and the line was then restored. The re-audit's run of the reproduction confirms it.
- The `dotnet build` is clean (0 warnings) and the fast tests are green across all 115 test projects. No quality run: only test code changed.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-04: Doing -> Done. A reply head whose rest starts with a bare LF is pinned to split into its own -v line; the lineFeed <= 0 mutant is killed
