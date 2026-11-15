---
id: AF-0751
title: Surviving mutant: a gs:// response header line beginning with whitespace can be accepted
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-14_1801.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary/Http/GcsHttpResponseHead.cs:ParseField-ge:surviving-mutant
reproduction: mutation Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary/Http/GcsHttpResponseHead.cs:31:>=
task: BL-5200
tasks: BL-5200
found: 2026-11-14
found-at: fc4d1268bd4c1bce123dba90334b07e141be698a
scorecard: 2026-11-14_1335.md
duplicate-of:
closed: 2026-11-14
closed-how: mechanical
closed-by: 2026-11-14_1801.md
---
# AF-0751 - Surviving mutant: a gs:// response header line beginning with whitespace can be accepted

## Summary

High finding from the quality auditor at `Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary/Http/GcsHttpResponseHead.cs:31`: Surviving mutant: a gs:// response header line beginning with whitespace can be accepted.

## Evidence

Location: `Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary/Http/GcsHttpResponseHead.cs:31`

Mutant 'line.AsSpan(0, colon).IndexOfAny(' ', '\t') >= 0' -> '> 0' survived. A field line starting with a space or tab (obsolete folding) would be accepted as a header named ' x' instead of failing MalformedHeaderField, which changes the transfer's exit code. SendAsync_MalformedField_FailsMalformedHeaderField has no DataRow with leading whitespace.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary/Http/GcsHttpResponseHead.cs:31:>= -Member ParseField -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-14 | 2026-11-14_1801.md | reproduces: no | Ran the -Site command: GcsHttpResponseHead.cs:31 'IndexOfAny(' ', '\t') >= 0' -> '> 0', outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-14: filed proposed.
- 2026-11-14: accepted -> closed. Re-audit 2026-11-14_1801.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
