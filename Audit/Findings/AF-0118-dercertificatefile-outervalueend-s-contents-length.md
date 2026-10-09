---
id: AF-0118
title: DerCertificateFile.OuterValueEnd's 'contents.Length < 2' can become '<= 2' with no test failing
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-09_0647.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Networking.UnitLibrary/DerCertificateFile.cs:OuterValueEnd-lt:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/DerCertificateFile.cs:85:<
task: BL-1868
tasks: BL-1868
found: 2026-10-08
found-at: cddb276d1d10fbb372f36a32cc1f588fd84c58e8
scorecard: 2026-10-08_2315.md
duplicate-of:
closed: 2026-10-09
closed-how: mechanical
closed-by: 2026-10-09_0647.md
---
# AF-0118 - DerCertificateFile.OuterValueEnd's 'contents.Length < 2' can become '<= 2' with no test failing

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/DerCertificateFile.cs:85`: DerCertificateFile.OuterValueEnd's 'contents.Length < 2' can become '<= 2' with no test failing. Reported by an auditor flagged unreliable in 2026-10-08_2315.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/DerCertificateFile.cs:85`

Mutant 'if (contents.Length < 2)' -> 'if (contents.Length <= 2)' survived. For a 2-byte DER file (a tag and a zero length, e.g. 30 00 or 04 00) the original reads the length and goes on to the tag check or certificate parse, while the mutant returns long.MaxValue. OuterValueError (line 73) then reports TlsFailureMessages.OpenSslNotEnoughData instead of OpenSslWrongTag or the parse's message, so the error text of the refusal changes. This is a narrow edge of a refusal message. No test feeds a 2-byte certificate file.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/DerCertificateFile.cs:85:< -Member OuterValueEnd -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-10-09 | 2026-10-09_0225.md | reproduces: yes | Ran the -Site reproduction: survived DerCertificateFile.cs:85 < [OuterValueEnd] 'contents.Length < 2' -> '<= 2'. It survived in the seed-0 sample as well.
- 2026-10-09 | 2026-10-09_0647.md | reproduces: no | Ran the -Site reproduction for Curl.Networking.UnitLibrary/DerCertificateFile.cs:85:< (member OuterValueEnd): outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-08: filed proposed.
- 2026-10-09: proposed -> accepted.
- 2026-10-09: accepted -> closed. Re-audit 2026-10-09_0647.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
