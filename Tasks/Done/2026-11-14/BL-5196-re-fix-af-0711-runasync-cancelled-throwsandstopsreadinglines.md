---
id: BL-5196
title: Re-fix AF-0711: RunAsync_Cancelled_ThrowsAndStopsReadingLines never checks that line reading stops
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.WebSocketRepl.UnitTests]
requirement: none
created: 2026-11-14
completed: 2026-11-14
---
# BL-5196 — Re-fix AF-0711: RunAsync_Cancelled_ThrowsAndStopsReadingLines never checks that line reading stops

## Goal

The defect the audit office reported as AF-0711 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0711 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Re-fix: the earlier task(s) BL-5070 reached Done, and a later re-audit by the quality auditor found the reproduction still reproduces:

- 2026-11-14 | 2026-11-14_1335.md | reproduces: yes | Select-String shows the test still only does Assert.ThrowsAsync<OperationCanceledException>(... session.RunAsync(new ScriptedLineReader(false), ...)); nothing checks that line reading stopped (no read count or reader state asserted).

Location: `Curl.Extension.WebSocketRepl.UnitTests/Session/WebSocketReplSessionTests.cs:173`

Location: `Curl.Extension.WebSocketRepl.UnitTests/Session/WebSocketReplSessionTests.cs:173`

Step 1. The only assertion is await Assert.ThrowsAsync<OperationCanceledException>(...). The ScriptedLineReader(false) it passes holds no lines and exposes no read count (its 'next' field is private), so a session that went on reading lines after cancellation would pass. The name promises 'StopsReadingLines'.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.WebSocketRepl.UnitTests/Session/WebSocketReplSessionTests.cs -Pattern 'RunAsync_Cancelled_ThrowsAndStopsReadingLines' -Context 0,8
```

- Expected: The body asserts the reader was not read after cancellation (lines left unread).
- Actual: The body asserts only the OperationCanceledException.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The session reads one line before it sees the cancellation (the fake reader ignores the token), so the test asserts at most one line was read; the reader now exposes LinesRead.

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-14: Doing -> Done. Test now asserts at most one line read after cancellation
