---
id: AF-0559
title: Surviving mutant: an S3 response line of exactly MaxLineLength bytes can be refused as too long without a test failing
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-03_1017.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Extended.Protocol.S3.UnitLibrary/Http/S3HttpConnectionReader.cs:ReadLineAsync-gt:surviving-mutant
reproduction: mutation Curl.Extended.Protocol.S3.UnitLibrary/Http/S3HttpConnectionReader.cs:29:>
task: BL-4707
tasks: BL-4707
found: 2026-11-03
found-at: ef78cb0569375bf8475eb1a2da931cfd224d52f4
scorecard: 2026-11-03_0025.md
duplicate-of:
closed: 2026-11-03
closed-how: mechanical
closed-by: 2026-11-03_1017.md
---
# AF-0559 - Surviving mutant: an S3 response line of exactly MaxLineLength bytes can be refused as too long without a test failing

## Summary

Medium finding from the quality auditor at `Curl.Extended.Protocol.S3.UnitLibrary/Http/S3HttpConnectionReader.cs:29`: Surviving mutant: an S3 response line of exactly MaxLineLength bytes can be refused as too long without a test failing. Reported by an auditor flagged unreliable in 2026-11-03_0025.md.

## Evidence

Location: `Curl.Extended.Protocol.S3.UnitLibrary/Http/S3HttpConnectionReader.cs:29`

Mutant 'line.WrittenCount + take > MaxLineLength' -> '>=' survived. The only limit test (SendAsync_LineLongerThanTheLimit_FailsLineTooLong) sends 'X-Long: ' plus MaxLineLength bytes, well past the limit; nothing pins a line of exactly the limit, which the mutant refuses with S3HttpFailure.LineTooLong.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extended.Protocol.S3.UnitLibrary/Http/S3HttpConnectionReader.cs:29:> -Member ReadLineAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-03 | 2026-11-03_1017.md | reproduces: no | Ran the -Site command: outcome killed (resolvedLine 29). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted.
- 2026-11-03: accepted -> closed. Re-audit 2026-11-03_1017.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
