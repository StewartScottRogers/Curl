---
id: AF-0124
title: ReadLineAsync_AnySplit_ReturnsWholeLinesThenTheRest accepts an empty rest
auditor: quality
severity: Low
status: accepted
reason: 
key: quality:Curl.Protocol.Http.UnitTests/HttpLineReaderTests.cs:ReadLineAsync_AnySplit_ReturnsWholeLinesThenTheRest:weak-assertion
reproduction: none
task: BL-1874
tasks: BL-1874
found: 2026-10-08
found-at: cddb276d1d10fbb372f36a32cc1f588fd84c58e8
scorecard: 2026-10-08_2315.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0124 - ReadLineAsync_AnySplit_ReturnsWholeLinesThenTheRest accepts an empty rest

## Summary

Low finding from the quality auditor at `Curl.Protocol.Http.UnitTests/HttpLineReaderTests.cs:40`: ReadLineAsync_AnySplit_ReturnsWholeLinesThenTheRest accepts an empty rest. Reported by an auditor flagged unreliable in 2026-10-08_2315.md.

## Evidence

Location: `Curl.Protocol.Http.UnitTests/HttpLineReaderTests.cs:40`

Line 40: 'Assert.IsTrue("rest".StartsWith(Encoding.ASCII.GetString(rest), StringComparison.Ordinal));'. Any prefix of "rest", including the empty string, passes, so a reader whose TakeRemaining dropped the bytes it had already buffered past the second line would pass for every chunk size, including the one-read row where all four bytes are buffered.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Protocol.Http.UnitTests/HttpLineReaderTests.cs -Pattern 'StartsWith\(Encoding'
```

- Expected: An exact assertion on the buffered rest for each chunk size (e.g. "rest" for the one-read row).
- Actual: HttpLineReaderTests.cs:40: Assert.IsTrue("rest".StartsWith(Encoding.ASCII.GetString(rest), StringComparison.Ordinal));

## Re-audits

- 2026-10-09 | 2026-10-09_0225.md | reproduces: yes | Select-String shows HttpLineReaderTests.cs:40 Assert.IsTrue("rest".StartsWith(Encoding.ASCII.GetString(rest), StringComparison.Ordinal)); "rest".StartsWith("") is true, so an empty rest passes.

## Log

- 2026-10-08: filed proposed.
- 2026-10-09: proposed -> accepted.
