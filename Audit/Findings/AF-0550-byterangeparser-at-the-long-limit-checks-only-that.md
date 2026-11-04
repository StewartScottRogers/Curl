---
id: AF-0550
title: ByteRangeParser at the long limit checks only that a range exists, never its positions
auditor: quality
severity: Low
status: closed
reason: Re-audit 2026-11-03_1615.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-11-03_1017.md, 2026-11-03_1615.md).
key: quality:Curl.Core.UnitTests/CoreParserAdversarialTests.cs:ByteRangeParser_PositionsAtTheLongLimit_NameARange:weak-assertion
reproduction: none
task: BL-4698
tasks: BL-4698
found: 2026-11-03
found-at: ef78cb0569375bf8475eb1a2da931cfd224d52f4
scorecard: 2026-11-03_0025.md
duplicate-of:
closed: 2026-11-03
closed-how: consecutive
closed-by: 2026-11-03_1017.md, 2026-11-03_1615.md
---
# AF-0550 - ByteRangeParser at the long limit checks only that a range exists, never its positions

## Summary

Low finding from the quality auditor at `Curl.Core.UnitTests/CoreParserAdversarialTests.cs:31`: ByteRangeParser at the long limit checks only that a range exists, never its positions. Reported by an auditor flagged unreliable in 2026-11-03_0025.md.

## Evidence

Location: `Curl.Core.UnitTests/CoreParserAdversarialTests.cs:31`

The body is 'Assert.IsTrue(parsed); Assert.IsNotNull(range);'. A range whose start or end overflowed or was clamped at 9223372036854775807 passes.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Core.UnitTests/CoreParserAdversarialTests.cs -Pattern 'Assert.IsNotNull\(range\)'
```

- Expected: The range's first and last positions are asserted per row.
- Actual: CoreParserAdversarialTests.cs:31: Assert.IsNotNull(range);

## Re-audits

- 2026-11-03 | 2026-11-03_1017.md | reproduces: no | No match for Assert.IsNotNull(range); the long-limit tests assert Assert.AreEqual(ByteRange.Bounded(0, 1), range) and Assert.AreEqual(ByteRange.FromOffset(5), range).
- 2026-11-03 | 2026-11-03_1615.md | reproduces: no | No 'Assert.IsNotNull(range)' match; CoreParserAdversarialTests asserts FirstBytePosition, LastBytePosition and SuffixLength.

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted.
- 2026-11-03: accepted -> closed. Re-audit 2026-11-03_1615.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-11-03_1017.md, 2026-11-03_1615.md).
