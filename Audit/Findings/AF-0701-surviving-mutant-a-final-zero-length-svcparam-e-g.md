---
id: AF-0701
title: Surviving mutant: a final zero-length SvcParam (e.g. no-default-alpn) can drop from the DoH HTTPS-record trace
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-12_2105.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Networking.UnitLibrary/DohTraceLines.HttpsRecord.cs:FirstHttpsRecordLines-ge:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/DohTraceLines.HttpsRecord.cs:140:>=
task: BL-4895
tasks: BL-4895
found: 2026-11-05
found-at: 4eba3c63638befcc6a63aaa31ddf5419922fa939
scorecard: 2026-11-05_2025.md
duplicate-of:
closed: 2026-11-12
closed-how: mechanical
closed-by: 2026-11-12_2105.md
---
# AF-0701 - Surviving mutant: a final zero-length SvcParam (e.g. no-default-alpn) can drop from the DoH HTTPS-record trace

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/DohTraceLines.HttpsRecord.cs:140`: Surviving mutant: a final zero-length SvcParam (e.g. no-default-alpn) can drop from the DoH HTTPS-record trace.

## Evidence

Location: `Curl.Networking.UnitLibrary/DohTraceLines.HttpsRecord.cs:140`

'while (length == 0 && record.Length - position >= 4)' mutated to '> 4' survived. A last SvcParam with an empty value (key 2, no-default-alpn) leaves exactly 4 bytes. With the mutant its end is never yielded, so the verbose DoH trace line for the record changes. No test ends a record with a zero-length parameter.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/DohTraceLines.HttpsRecord.cs:140:>= -Member FirstHttpsRecordLines -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-12 | 2026-11-12_2105.md | reproduces: no | Ran the -Site command: DohTraceLines.HttpsRecord.cs:140 'record.Length - position >= 4' -> '> 4' was killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-05: filed proposed.
- 2026-11-12: accepted -> closed. Re-audit 2026-11-12_2105.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
