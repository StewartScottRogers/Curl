---
id: BL-4775
title: Fix AF-0611: ProgressMeterFields.Speed's doc says it computes 'without overflowing' and caps at long.MaxValue; the under-one-microsecond branch multiplies unchecked
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Output.UnitLibrary]
requirement: none
created: 2026-11-03
completed:
---
# BL-4775 — Fix AF-0611: ProgressMeterFields.Speed's doc says it computes 'without overflowing' and caps at long.MaxValue; the under-one-microsecond branch multiplies unchecked

## Goal

The defect the audit office reported as AF-0611 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0611 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0611-progressmeterfields-speed-s-doc-says-it-computes-w.md`.

Location: `Curl.Output.UnitLibrary/ProgressMeterFields.cs:83`

Location: `Curl.Output.UnitLibrary/ProgressMeterFields.cs:83`

Sampled method (seed 985570). Doc: "Computes an average speed in bytes per second as <c>trspeed</c> does, without overflowing: under one microsecond the size counts as sent in one." and "<returns>The speed, capped at <see cref=\"long.MaxValue\" />.</returns>". Line 93: "if (microseconds < 1) { return size * 1000000; }". No range check and no checked context (CheckForOverflowUnderflow is not set in Directory.Build.props or the csproj). Any size of 9,223,372,036,855 bytes or more wraps to a wrong value instead of being capped. The arithmetic matches curl's trspeed, so only the doc's 'without overflowing' and 'capped' are untrue.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Output.UnitLibrary/ProgressMeterFields.cs -Pattern 'without$|overflowing|capped at|return size \* 1000000;'
```

- Expected: Either the doc says the sub-microsecond branch can overflow as curl's does, or that branch caps at long.MaxValue.
- Actual: 83: '... as trspeed does, without' / 84: 'overflowing: ...' / capped at long.MaxValue / 93: 'return size * 1000000;' unchecked

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
