---
id: AF-0771
title: Surviving mutant: the production DoH resolver can accept a plain http:// --doh-url that curl's release build refuses
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-15_0222.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Engine.UnitLibrary/EngineComposition.Dns.cs:CreateDnsResolver-false:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/EngineComposition.Dns.cs:37:false
task: BL-5220
tasks: BL-5220
found: 2026-11-14
found-at: 92f6fc980babe08d1ebff5f02d7e076e42c67fd7
scorecard: 2026-11-14_1801.md
duplicate-of:
closed: 2026-11-15
closed-how: mechanical
closed-by: 2026-11-15_0222.md
---
# AF-0771 - Surviving mutant: the production DoH resolver can accept a plain http:// --doh-url that curl's release build refuses

## Summary

High finding from the quality auditor at `Curl.Engine.UnitLibrary/EngineComposition.Dns.cs:37`: Surviving mutant: the production DoH resolver can accept a plain http:// --doh-url that curl's release build refuses. Reported by an auditor flagged unreliable in 2026-11-14_1801.md.

## Evidence

Location: `Curl.Engine.UnitLibrary/EngineComposition.Dns.cs:37`

Mutant 'bool allowsPlainHttpDohUrl = false' -> 'true' survived. The production caller, EngineComposition.Transports.cs:142, calls CreateDnsResolver(network, tls, timeProvider, tcpDialer, resolverEvents, easyErrorText, traces, takeDohConnectionId: ...) and relies on the default. The doc comment says false gives curl 8.22.0's release build, where a non-https DoH URL becomes an UnusableDohUrlResolver. Under the mutant every http:// DoH URL is used: the request goes out and the exit code changes. No Engine test builds the transports with an http:// --doh-url.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/EngineComposition.Dns.cs:37:false -Member CreateDnsResolver -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-15 | 2026-11-15_0222.md | reproduces: no | Ran the -Site command: resolvedLine 37, outcome killed (allowsPlainHttpDohUrl = true). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-14: filed proposed.
- 2026-11-15: accepted -> closed. Re-audit 2026-11-15_0222.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
