---
id: AF-0153
title: KDC proxy reply limit `reply.Length > MaximumReplyLength` can become `>=` with no test failing
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-11_1638.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Networking.UnitLibrary/KerberosKdcProxyHttpsTransport.cs:ReadToEndAsync-gt:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/KerberosKdcProxyHttpsTransport.cs:133:>
task: BL-2046
tasks: BL-2046
found: 2026-10-11
found-at: 22f394602532a6922adbc7fa91b3739b62d2112a
scorecard: 2026-10-11_0051.md
duplicate-of:
closed: 2026-10-11
closed-how: mechanical
closed-by: 2026-10-11_1638.md
---
# AF-0153 - KDC proxy reply limit `reply.Length > MaximumReplyLength` can become `>=` with no test failing

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/KerberosKdcProxyHttpsTransport.cs:133`: KDC proxy reply limit `reply.Length > MaximumReplyLength` can become `>=` with no test failing.

## Evidence

Location: `Curl.Networking.UnitLibrary/KerberosKdcProxyHttpsTransport.cs:133`

Invoke-MutationTest.ps1 -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0 -ExcludeBaselineFailures: `if (reply.Length > MaximumReplyLength)` mutated to `if (reply.Length >= MaximumReplyLength)` survived. MaximumReplyLength is 1024 * 1024. The mutant refuses a KDC proxy reply of exactly 1 MiB with an IOException, so Kerberos through the KDC proxy fails where it should succeed. No test sends a reply at the limit. This is a refusal decision, an off-by-one at a limit no test pins.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/KerberosKdcProxyHttpsTransport.cs:133:> -Member ReadToEndAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-10-11 | 2026-10-11_1638.md | reproduces: no | Ran -Site Curl.Networking.UnitLibrary/KerberosKdcProxyHttpsTransport.cs:133:> -Member ReadToEndAsync -ExcludeBaselineFailures -TimeoutSeconds 600: resolvedLine 133, outcome killed (killed 1, survived 0). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-11: filed proposed.
- 2026-10-11: proposed -> accepted.
- 2026-10-11: accepted -> closed. Re-audit 2026-10-11_1638.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
