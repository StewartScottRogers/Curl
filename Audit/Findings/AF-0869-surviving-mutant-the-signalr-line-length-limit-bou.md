---
id: AF-0869
title: Surviving mutant: the SignalR line-length limit boundary is untested
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-19_0134.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Extended.Protocol.SignalR.UnitLibrary/Transport/BufferedConnectionReader.cs:ReadLineAsync-ge:surviving-mutant
reproduction: mutation Curl.Extended.Protocol.SignalR.UnitLibrary/Transport/BufferedConnectionReader.cs:28:>=
task: BL-5511
tasks: BL-5511
found: 2026-11-18
found-at: 4f902456d88c3fda856546dbe0c9a7674b1f04c4
scorecard: 2026-11-18_0810.md
duplicate-of:
closed: 2026-11-19
closed-how: mechanical
closed-by: 2026-11-19_0134.md
---
# AF-0869 - Surviving mutant: the SignalR line-length limit boundary is untested

## Summary

Medium finding from the quality auditor at `Curl.Extended.Protocol.SignalR.UnitLibrary/Transport/BufferedConnectionReader.cs:28`: Surviving mutant: the SignalR line-length limit boundary is untested. Reported by an auditor flagged unreliable in 2026-11-18_0810.md.

## Evidence

Location: `Curl.Extended.Protocol.SignalR.UnitLibrary/Transport/BufferedConnectionReader.cs:28`

Mutant 'if (end - start >= MaxLineLength)' -> '>' survived. A head line of exactly MaxLineLength (64 KiB) bytes with no LF yet is refused unmutated but read further under the mutant. ReadHeadAsync_LineLongerThanTheLimit_Throws uses a line past the limit only, so the refusal boundary can move unnoticed.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extended.Protocol.SignalR.UnitLibrary/Transport/BufferedConnectionReader.cs:28:>= -Member ReadLineAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-19 | 2026-11-19_0134.md | reproduces: no | Targeted run Curl.Extended.Protocol.SignalR.UnitLibrary/Transport/BufferedConnectionReader.cs:28:>= (resolvedLine 28) gave outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-18: filed proposed.
- 2026-11-19: accepted -> closed. Re-audit 2026-11-19_0134.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
