---
id: AF-0427
title: DerCertificateFile.LongFormValueEnd's headerLength > contents.Length can become >= with no test failing
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-25_0941.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Networking.UnitLibrary/DerCertificateFile.cs:LongFormValueEnd-gt:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/DerCertificateFile.cs:109:>
task: BL-3493
tasks: BL-3493
found: 2026-10-24
found-at: 456765a13655caedad49326c988f9e4eec86da8b
scorecard: 2026-10-24_2106.md
duplicate-of:
closed: 2026-10-25
closed-how: mechanical
closed-by: 2026-10-25_0941.md
---
# AF-0427 - DerCertificateFile.LongFormValueEnd's headerLength > contents.Length can become >= with no test failing

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/DerCertificateFile.cs:109`: DerCertificateFile.LongFormValueEnd's headerLength > contents.Length can become >= with no test failing.

## Evidence

Location: `Curl.Networking.UnitLibrary/DerCertificateFile.cs:109`

Sampled mutant (seed 0) survived: 'if (headerLength > contents.Length)' -> 'if (headerLength >= contents.Length)'. When a DER file ends exactly after a long-form length header (e.g. 30 81 00, or 04 81 00), the original computes valueEnd == contents.Length and OuterValueError goes on to the tag check / certificate load (OpenSslWrongTag); the mutant returns long.MaxValue and reports OpenSslNotEnoughData. The error message curl prints for such a --cert/--cacert file changes, and no test pins the boundary where the header fills the file.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/DerCertificateFile.cs:109:> -Member LongFormValueEnd -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-10-25 | 2026-10-25_0941.md | reproduces: no | Ran the -Site reproduction (DerCertificateFile.cs:109:>, -Member LongFormValueEnd): resolvedLine 109, 'if (headerLength > contents.Length)' -> '>=', outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-24: filed proposed.
- 2026-10-25: proposed -> accepted.
- 2026-10-25: accepted -> closed. Re-audit 2026-10-25_0941.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
