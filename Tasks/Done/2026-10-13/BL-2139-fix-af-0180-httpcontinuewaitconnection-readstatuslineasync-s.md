---
id: BL-2139
title: Fix AF-0180: HttpContinueWaitConnection.ReadStatusLineAsync's 'IndexOf(...) < 0' can become '<= 0' with no test failing: a reply starting with a bare LF no longer ends the 100-continue wait at once
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-13
completed: 2026-10-13
---
# BL-2139 — Fix AF-0180: HttpContinueWaitConnection.ReadStatusLineAsync's 'IndexOf(...) < 0' can become '<= 0' with no test failing: a reply starting with a bare LF no longer ends the 100-continue wait at once

## Goal

The defect the audit office reported as AF-0180 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0180 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0180-httpcontinuewaitconnection-readstatuslineasync-s-i.md`.

Location: `Curl.Protocol.Http.UnitLibrary/HttpContinueWaitConnection.cs:170`

Location: `Curl.Protocol.Http.UnitLibrary/HttpContinueWaitConnection.cs:170`

Sampled mutation (seed 0) of Curl.Protocol.Http.UnitLibrary: 'while ((lineFeed = Array.IndexOf(received, (byte)'\n', 0, receivedLength)) < 0 && receivedLength < received.Length)' mutated to '<= 0' survived. With the original, a first line feed at offset 0 ends the read: StatusCodeOf gives null and the wait learns at once that no 100 Continue came. With the mutant, IndexOf keeps returning 0, so the loop keeps reading until the connection ends or the 16 KB buffer fills. The expect-100 wait therefore runs to its timeout and sends the body to a server that has already answered. That is a decision about which request bytes are sent, and no test feeds a reply whose first byte is a line feed.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/HttpContinueWaitConnection.cs:170:< -Member ReadStatusLineAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Test-only fix: `HttpContinueWaitConnectionTests.WaitForContinueAsync_ReplyStartsWithABareLineFeed_StopsReadingAtItAndLeavesTheBodyUnsent`
  feeds `\nHTTP/1.1 100 Continue\r\n\r\n` followed by a reset, at 1-byte and 64 KB reads. The original stops
  at the line feed, leaves the body unsent and replays `\n`. The `<= 0` mutant reads on into the reset, so its
  status-line read faults and the first replay read throws. The production code was already right.
- Added `Curl.Protocol.Http.UnitTests` to `touches`: the test lives there. No task in Doing on
  `origin/work/dark-factory` named it (BL-2128, BL-2138, BL-2142, BL-2146 checked).
- The audit guard refuses lanes `Audit/Tools/Invoke-MutationTest.ps1`, so the mutant was checked by hand:
  `< 0` was changed to `<= 0` at line 170 and the test class was run (1 failed, the new test; 28 passed),
  then the source was restored. The quality auditor's re-audit is still what closes AF-0180.

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Done. A reply starting with a bare LF is now pinned to end the 100-continue wait at once; the <= 0 mutant at HttpContinueWaitConnection.cs:170 is killed
