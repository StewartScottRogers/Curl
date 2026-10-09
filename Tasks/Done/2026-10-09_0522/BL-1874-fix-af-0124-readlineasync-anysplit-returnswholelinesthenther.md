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
completed: 2026-10-09
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The test now takes the expected rest as a second `DataRow` value and asserts it exactly with `Assert.AreEqual`. `HttpLineReader.ReadLineAsync` scans its buffer before reading again, so 1-byte and 3-byte reads have read nothing past the second line (rest is empty) and the single 64 KiB read has buffered all of `rest`. A `TakeRemaining` that dropped buffered bytes now fails the one-read row.
- The reproduction `Select-String ... -Pattern 'StartsWith(Encoding'` now finds nothing. `Curl.Protocol.Http.UnitTests` builds clean and passes (1961 passed, 18 skipped); only that test project changed.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Done. Rest assertion is exact per chunk size; reproduction no longer matches; Http unit tests green
