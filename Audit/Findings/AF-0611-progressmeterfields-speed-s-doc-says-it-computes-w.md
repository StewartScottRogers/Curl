---
id: AF-0611
title: ProgressMeterFields.Speed's doc says it computes 'without overflowing' and caps at long.MaxValue; the under-one-microsecond branch multiplies unchecked
auditor: truthfulness
severity: Low
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: truthfulness:Curl.Output.UnitLibrary/ProgressMeterFields.cs:Speed:false-doc-comment
reproduction: none
task: BL-4775
tasks: BL-4775
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0611 - ProgressMeterFields.Speed's doc says it computes 'without overflowing' and caps at long.MaxValue; the under-one-microsecond branch multiplies unchecked

## Summary

Low finding from the truthfulness auditor at `Curl.Output.UnitLibrary/ProgressMeterFields.cs:83`: ProgressMeterFields.Speed's doc says it computes 'without overflowing' and caps at long.MaxValue; the under-one-microsecond branch multiplies unchecked.

## Evidence

Location: `Curl.Output.UnitLibrary/ProgressMeterFields.cs:83`

Sampled method (seed 985570). Doc: "Computes an average speed in bytes per second as <c>trspeed</c> does, without overflowing: under one microsecond the size counts as sent in one." and "<returns>The speed, capped at <see cref=\"long.MaxValue\" />.</returns>". Line 93: "if (microseconds < 1) { return size * 1000000; }". No range check and no checked context (CheckForOverflowUnderflow is not set in Directory.Build.props or the csproj). Any size of 9,223,372,036,855 bytes or more wraps to a wrong value instead of being capped. The arithmetic matches curl's trspeed, so only the doc's 'without overflowing' and 'capped' are untrue.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Output.UnitLibrary/ProgressMeterFields.cs -Pattern 'without$|overflowing|capped at|return size \* 1000000;'
```

- Expected: Either the doc says the sub-microsecond branch can overflow as curl's does, or that branch caps at long.MaxValue.
- Actual: 83: '... as trspeed does, without' / 84: 'overflowing: ...' / capped at long.MaxValue / 93: 'return size * 1000000;' unchecked

## Re-audits

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
