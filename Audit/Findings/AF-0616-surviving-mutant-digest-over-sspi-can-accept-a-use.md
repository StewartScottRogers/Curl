---
id: AF-0616
title: Surviving mutant: Digest over SSPI can accept a user name whose first character is a control character
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-03_2203.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Authentication.UnitLibrary/DigestAuthenticator.cs:CreateSspiAnswer-ge:surviving-mutant
reproduction: mutation Curl.Authentication.UnitLibrary/DigestAuthenticator.cs:101:>=
task: BL-4789
tasks: BL-4789
found: 2026-11-03
found-at: 7410f102c809d5232e4f9b210b3ac32486848c27
scorecard: 2026-11-03_1615.md
duplicate-of:
closed: 2026-11-03
closed-how: mechanical
closed-by: 2026-11-03_2203.md
---
# AF-0616 - Surviving mutant: Digest over SSPI can accept a user name whose first character is a control character

## Summary

High finding from the quality auditor at `Curl.Authentication.UnitLibrary/DigestAuthenticator.cs:101`: Surviving mutant: Digest over SSPI can accept a user name whose first character is a control character. Reported by an auditor flagged unreliable in 2026-11-03_1615.md.

## Evidence

Location: `Curl.Authentication.UnitLibrary/DigestAuthenticator.cs:101`

Mutant 'user.AsSpan().IndexOfAnyInRange('\0', '\u001f') >= 0' -> '> 0' survived (seed 791054 sample). With it, a user name starting with a control byte (e.g. '\u0001u') no longer fails with exit 94 'An authentication function returned an error' and a Digest answer is sent instead. Cgr077Tests' DataRows put the control character later in the name ('hello\n\r', 'us\tr'), never at index 0. The exit code and the request bytes change.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Authentication.UnitLibrary/DigestAuthenticator.cs:101:>= -Member CreateSspiAnswer -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-03 | 2026-11-03_2203.md | reproduces: no | Ran Invoke-MutationTest.ps1 -Site Curl.Authentication.UnitLibrary/DigestAuthenticator.cs:101:>= -Member CreateSspiAnswer -ExcludeBaselineFailures -TimeoutSeconds 600: resolvedLine 101, outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
- 2026-11-03: accepted -> closed. Re-audit 2026-11-03_2203.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
