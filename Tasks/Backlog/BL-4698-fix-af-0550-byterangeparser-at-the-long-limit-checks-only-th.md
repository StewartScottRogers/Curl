---
id: BL-4698
title: Fix AF-0550: ByteRangeParser at the long limit checks only that a range exists, never its positions
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitTests]
requirement: none
created: 2026-11-03
completed:
---
# BL-4698 — Fix AF-0550: ByteRangeParser at the long limit checks only that a range exists, never its positions

## Goal

The defect the audit office reported as AF-0550 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0550 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0550-byterangeparser-at-the-long-limit-checks-only-that.md`.

Location: `Curl.Core.UnitTests/CoreParserAdversarialTests.cs:31`

Location: `Curl.Core.UnitTests/CoreParserAdversarialTests.cs:31`

The body is 'Assert.IsTrue(parsed); Assert.IsNotNull(range);'. A range whose start or end overflowed or was clamped at 9223372036854775807 passes.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Core.UnitTests/CoreParserAdversarialTests.cs -Pattern 'Assert.IsNotNull\(range\)'
```

- Expected: The range's first and last positions are asserted per row.
- Actual: CoreParserAdversarialTests.cs:31: Assert.IsNotNull(range);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
