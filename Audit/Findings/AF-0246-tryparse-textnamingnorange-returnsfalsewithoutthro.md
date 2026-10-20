---
id: AF-0246
title: TryParse_TextNamingNoRange_ReturnsFalseWithoutThrowing asserts nothing: both assertions are commented out
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-19_2134.md: the reproduction no longer reproduces.
key: quality:Curl.Core.UnitTests/ByteRangeParserTests.cs:TryParse_TextNamingNoRange_ReturnsFalseWithoutThrowing:no-assertion
reproduction: none
task: BL-2757
tasks: BL-2757
found: 2026-10-18
found-at: 19ea231be8b4d749462d15058a3c6b97c7e70836
scorecard: 2026-10-18_1045.md
duplicate-of:
closed: 2026-10-19
closed-how: reliable-reaudit
closed-by: 2026-10-19_2134.md
---
# AF-0246 - TryParse_TextNamingNoRange_ReturnsFalseWithoutThrowing asserts nothing: both assertions are commented out

## Summary

Medium finding from the quality auditor at `Curl.Core.UnitTests/ByteRangeParserTests.cs:76`: TryParse_TextNamingNoRange_ReturnsFalseWithoutThrowing asserts nothing: both assertions are commented out. Reported by an auditor flagged unreliable in 2026-10-18_1045.md.

## Evidence

Location: `Curl.Core.UnitTests/ByteRangeParserTests.cs:76`

Step 0 candidate (no-assertion), read in full. The body is `var parsed = Parse(rangeText, out ByteRange? range, false, null);` followed by `// Assert.IsFalse(parsed);` and `// Assert.IsNull(range);` (lines 80-81). The name promises that each of 11 malformed range texts ('3-1', 'abc', '-0', '9223372036854775808-', ...) returns false. A parser that accepted any of them, or returned a range for them, would still pass.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Core.UnitTests/ByteRangeParserTests.cs -Pattern '^\s*// Assert\.'
```

- Expected: No match: the test asserts IsFalse(parsed) and IsNull(range).
- Actual: ByteRangeParserTests.cs:80: // Assert.IsFalse(parsed); and ByteRangeParserTests.cs:81: // Assert.IsNull(range);

## Re-audits

- 2026-10-18 | 2026-10-18_1905.md | reproduces: no | Ran the Select-String: no match. TryParse_TextNamingNoRange_ReturnsFalseWithoutThrowing now has live Assert.IsFalse(parsed) and Assert.IsNull(range) (lines 80-81).
- 2026-10-19 | 2026-10-19_2134.md | reproduces: no | Select-String found no commented-out assertion. TryParse_TextNamingNoRange_ReturnsFalseWithoutThrowing (ByteRangeParserTests.cs:76) now asserts Assert.IsFalse(parsed) and Assert.IsNull(range).

## Log

- 2026-10-18: filed proposed.
- 2026-10-18: proposed -> accepted.
- 2026-10-19: accepted -> closed. Re-audit 2026-10-19_2134.md: the reproduction no longer reproduces.
