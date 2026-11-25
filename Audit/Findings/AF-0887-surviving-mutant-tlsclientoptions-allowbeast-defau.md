---
id: AF-0887
title: Surviving mutant: TlsClientOptions' AllowBeast default can flip to true without a test noticing
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-19_1633.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Networking.UnitLibrary/TlsClientOptions.cs:TlsClientOptions-false:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/TlsClientOptions.cs:232:false
task: BL-5558
tasks: BL-5558
found: 2026-11-19
found-at: 6e68cb60b626d421113f6d7d7f35c2a46bbc3b46
scorecard: 2026-11-19_0602.md
duplicate-of:
closed: 2026-11-19
closed-how: mechanical
closed-by: 2026-11-19_1633.md
---
# AF-0887 - Surviving mutant: TlsClientOptions' AllowBeast default can flip to true without a test noticing

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/TlsClientOptions.cs:232`: Surviving mutant: TlsClientOptions' AllowBeast default can flip to true without a test noticing.

## Evidence

Location: `Curl.Networking.UnitLibrary/TlsClientOptions.cs:232`

Mutant bool AllowBeast = false, -> bool AllowBeast = true, survived all Curl.Networking.UnitTests (seed 446316). HandBuiltTlsProvider.InsertsEmptyFragment returns !options.AllowBeast, so every TlsClientOptions built without naming AllowBeast would turn off the BEAST empty-fragment mitigation that curl applies unless --ssl-allow-beast is given. That changes the TLS 1.0 CBC record bytes sent. No test checks the default.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/TlsClientOptions.cs:232:false -Member TlsClientOptions -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-19 | 2026-11-19_1633.md | reproduces: no | Ran the -Site command: TlsClientOptions.cs:232 'bool AllowBeast = false,' -> 'true' at resolvedLine 232, outcome killed (killed=1, survived=0). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-19: filed proposed.
- 2026-11-19: accepted -> closed. Re-audit 2026-11-19_1633.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
