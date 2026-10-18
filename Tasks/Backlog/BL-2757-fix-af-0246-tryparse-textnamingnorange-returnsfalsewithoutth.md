---
id: BL-2757
title: Fix AF-0246: TryParse_TextNamingNoRange_ReturnsFalseWithoutThrowing asserts nothing: both assertions are commented out
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitTests]
requirement: none
created: 2026-10-18
completed:
---
# BL-2757 — Fix AF-0246: TryParse_TextNamingNoRange_ReturnsFalseWithoutThrowing asserts nothing: both assertions are commented out

## Goal

The defect the audit office reported as AF-0246 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0246 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0246-tryparse-textnamingnorange-returnsfalsewithoutthro.md`.

Location: `Curl.Core.UnitTests/ByteRangeParserTests.cs:76`

Location: `Curl.Core.UnitTests/ByteRangeParserTests.cs:76`

Step 0 candidate (no-assertion), read in full. The body is `var parsed = Parse(rangeText, out ByteRange? range, false, null);` followed by `// Assert.IsFalse(parsed);` and `// Assert.IsNull(range);` (lines 80-81). The name promises that each of 11 malformed range texts ('3-1', 'abc', '-0', '9223372036854775808-', ...) returns false. A parser that accepted any of them, or returned a range for them, would still pass.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Core.UnitTests/ByteRangeParserTests.cs -Pattern '^\s*// Assert\.'
```

- Expected: No match: the test asserts IsFalse(parsed) and IsNull(range).
- Actual: ByteRangeParserTests.cs:80: // Assert.IsFalse(parsed); and ByteRangeParserTests.cs:81: // Assert.IsNull(range);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-18: Created.
