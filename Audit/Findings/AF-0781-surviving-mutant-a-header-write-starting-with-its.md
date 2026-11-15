---
id: AF-0781
title: Surviving mutant: a header write starting with its line feed can lose that line from the recorded headers
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-15_1159.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Engine.UnitLibrary/HeaderRecordingStream.cs:Record-ge:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/HeaderRecordingStream.cs:84:>=
task: BL-5232
tasks: BL-5232
found: 2026-11-15
found-at: d04b275ae7a1546a4d0feed894da1ba520945cf5
scorecard: 2026-11-15_0222.md
duplicate-of:
closed: 2026-11-15
closed-how: mechanical
closed-by: 2026-11-15_1159.md
---
# AF-0781 - Surviving mutant: a header write starting with its line feed can lose that line from the recorded headers

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/HeaderRecordingStream.cs:84`: Surviving mutant: a header write starting with its line feed can lose that line from the recorded headers.

## Evidence

Location: `Curl.Engine.UnitLibrary/HeaderRecordingStream.cs:84`

while ((lineFeed = rest.IndexOf((byte)'\n')) >= 0) mutated to > 0 survived. A write whose first byte is '\n' (a bare-LF blank line ending a header block, or a CRLF split across writes) is no longer split there, so TransferHeaderStore.RecordLine gets merged or missing lines, which the header API and %header{} read.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/HeaderRecordingStream.cs:84:>= -Member Record -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-15 | 2026-11-15_1159.md | reproduces: no | Ran the -Site reproduction: HeaderRecordingStream.cs:84 >= resolved at line 84, outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-15: filed proposed.
- 2026-11-15: accepted -> closed. Re-audit 2026-11-15_1159.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
