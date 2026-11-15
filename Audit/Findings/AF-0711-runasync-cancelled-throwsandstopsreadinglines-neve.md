---
id: AF-0711
title: RunAsync_Cancelled_ThrowsAndStopsReadingLines never checks that line reading stops
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Extension.WebSocketRepl.UnitTests/Session/WebSocketReplSessionTests.cs:RunAsync_Cancelled_ThrowsAndStopsReadingLines:name-lies
reproduction: none
task: BL-5196
tasks: BL-5070, BL-5196
found: 2026-11-12
found-at: acc2af773fb64825b3b57d9470bb6f96176fb127
scorecard: 2026-11-12_2105.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0711 - RunAsync_Cancelled_ThrowsAndStopsReadingLines never checks that line reading stops

## Summary

Medium finding from the quality auditor at `Curl.Extension.WebSocketRepl.UnitTests/Session/WebSocketReplSessionTests.cs:173`: RunAsync_Cancelled_ThrowsAndStopsReadingLines never checks that line reading stops. Reported by an auditor flagged unreliable in 2026-11-12_2105.md.

## Evidence

Location: `Curl.Extension.WebSocketRepl.UnitTests/Session/WebSocketReplSessionTests.cs:173`

Step 1. The only assertion is await Assert.ThrowsAsync<OperationCanceledException>(...). The ScriptedLineReader(false) it passes holds no lines and exposes no read count (its 'next' field is private), so a session that went on reading lines after cancellation would pass. The name promises 'StopsReadingLines'.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.WebSocketRepl.UnitTests/Session/WebSocketReplSessionTests.cs -Pattern 'RunAsync_Cancelled_ThrowsAndStopsReadingLines' -Context 0,8
```

- Expected: The body asserts the reader was not read after cancellation (lines left unread).
- Actual: The body asserts only the OperationCanceledException.

## Re-audits

- 2026-11-13 | 2026-11-13_2321.md | reproduces: yes | The body is still only await Assert.ThrowsAsync<OperationCanceledException>(...session.RunAsync(new ScriptedLineReader(false), ...)); ScriptedLineReader keeps no count of reads, so nothing checks that line reading stops.
- 2026-11-14 | 2026-11-14_1335.md | reproduces: yes | Select-String shows the test still only does Assert.ThrowsAsync<OperationCanceledException>(... session.RunAsync(new ScriptedLineReader(false), ...)); nothing checks that line reading stopped (no read count or reader state asserted).
- 2026-11-14 | 2026-11-14_1801.md | reproduces: no | Ran the Select-String. RunAsync_Cancelled_ThrowsAndStopsReadingLines now ends with Assert.IsLessThanOrEqualTo(1, reader.LinesRead) after the ThrowsAsync<OperationCanceledException>. The reader is scripted with two lines ('never read', '/ping') and does not end after them, so reading past the first line fails the test: line reading is checked.

## Log

- 2026-11-12: filed proposed.
- 2026-11-13: proposed -> accepted.
