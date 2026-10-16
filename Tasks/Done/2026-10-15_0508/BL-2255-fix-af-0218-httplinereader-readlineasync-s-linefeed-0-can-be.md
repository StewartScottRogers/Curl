---
id: BL-2255
title: Fix AF-0218: HttpLineReader.ReadLineAsync's 'lineFeed >= 0' can become 'lineFeed > 0' with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-14
completed: 2026-10-14
---
# BL-2255 — Fix AF-0218: HttpLineReader.ReadLineAsync's 'lineFeed >= 0' can become 'lineFeed > 0' with no test failing

## Goal

The defect the audit office reported as AF-0218 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0218 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0218-httplinereader-readlineasync-s-linefeed-0-can-beco.md`.

Location: `Curl.Protocol.Http.UnitLibrary/HttpLineReader.cs:70`

Location: `Curl.Protocol.Http.UnitLibrary/HttpLineReader.cs:70`

Sampled run (seed 0) of Curl.Protocol.Http.UnitLibrary: mutant 'if (lineFeed >= 0)' -> 'if (lineFeed > 0)' in ReadLineAsync survived. lineFeed is an absolute buffer index, so it is 0 when a line starts at buffer[0] and is a bare LF. That happens for a response whose first byte is LF, or for an LF-only blank line left at the front after MakeRoom compacts a full buffer. Under the mutant that line is not returned: it is run through RejectHttp09 as a status line, or merged with the next line, so the head ends in the wrong place. No test puts a lone LF at buffer index 0.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/HttpLineReader.cs:70:>= -Member ReadLineAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added `Curl.Protocol.Http.UnitTests` to `touches`: the fix is a test, and no other task in Doing on `origin/work/dark-factory` names that project.
- Fix: `HttpLineReaderTests.ReadLineAsync_BareLineFeedAtTheFrontOfTheBuffer_ReturnsItAsALineOfItsOwn` scripts `\nab\n` (a bare LF at buffer index 0) and asserts the lines `\n` then `ab\n`, for an ordinary line with 1-byte and single reads and for a status line. No production change: the code was right, only the test was missing.
- A lane may not run `Audit/Tools/Invoke-MutationTest.ps1` (audit path), so the mutant was applied by hand instead: with `if (lineFeed > 0)` all 3 new rows fail (Failed 3, Passed 6 of `HttpLineReaderTests`); with the original `>= 0` all pass. The quality auditor's re-audit confirms it with the tool.

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
- 2026-10-14: Doing -> Done. A test now puts a bare LF at buffer index 0, so the lineFeed > 0 mutant in HttpLineReader.ReadLineAsync fails 3 tests
