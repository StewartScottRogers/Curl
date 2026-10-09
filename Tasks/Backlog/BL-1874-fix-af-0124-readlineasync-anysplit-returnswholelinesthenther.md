---
id: BL-1874
title: Fix AF-0124: ReadLineAsync_AnySplit_ReturnsWholeLinesThenTheRest accepts an empty rest
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-09
completed:
---
# BL-1874 — Fix AF-0124: ReadLineAsync_AnySplit_ReturnsWholeLinesThenTheRest accepts an empty rest

## Goal

The defect the audit office reported as AF-0124 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0124 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0124-readlineasync-anysplit-returnswholelinesthentheres.md`.

Location: `Curl.Protocol.Http.UnitTests/HttpLineReaderTests.cs:40`

Location: `Curl.Protocol.Http.UnitTests/HttpLineReaderTests.cs:40`

Line 40: 'Assert.IsTrue("rest".StartsWith(Encoding.ASCII.GetString(rest), StringComparison.Ordinal));'. Any prefix of "rest", including the empty string, passes, so a reader whose TakeRemaining dropped the bytes it had already buffered past the second line would pass for every chunk size, including the one-read row where all four bytes are buffered.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Protocol.Http.UnitTests/HttpLineReaderTests.cs -Pattern 'StartsWith\(Encoding'
```

- Expected: An exact assertion on the buffered rest for each chunk size (e.g. "rest" for the one-read row).
- Actual: HttpLineReaderTests.cs:40: Assert.IsTrue("rest".StartsWith(Encoding.ASCII.GetString(rest), StringComparison.Ordinal));

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-09: Created.
