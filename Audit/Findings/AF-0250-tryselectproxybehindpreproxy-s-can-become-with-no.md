---
id: AF-0250
title: TrySelectProxyBehindPreProxy's && can become || with no test failing, refusing every HTTP proxy behind a pre-proxy with exit 5
auditor: quality
severity: High
status: accepted
reason: 
key: quality:Curl.Engine.UnitLibrary/TransferProxySelection.cs:TrySelectProxyBehindPreProxy-and:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/TransferProxySelection.cs:184:&&
task: BL-2761
tasks: BL-2761
found: 2026-10-18
found-at: 19ea231be8b4d749462d15058a3c6b97c7e70836
scorecard: 2026-10-18_1045.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0250 - TrySelectProxyBehindPreProxy's && can become || with no test failing, refusing every HTTP proxy behind a pre-proxy with exit 5

## Summary

High finding from the quality auditor at `Curl.Engine.UnitLibrary/TransferProxySelection.cs:184`: TrySelectProxyBehindPreProxy's && can become || with no test failing, refusing every HTTP proxy behind a pre-proxy with exit 5. Reported by an auditor flagged unreliable in 2026-10-18_1045.md.

## Evidence

Location: `Curl.Engine.UnitLibrary/TransferProxySelection.cs:184`

Engine sample, seed 0: survived. Original `if (selected is not null && !IsHttpKind(selected.Kind))`, mutated `||`. With the mutant, any selected proxy, including an HTTP one, fails with CouldntResolveProxy "Having a SOCKS pre-proxy and proxy is not supported". A null selection would throw a NullReferenceException. No test runs a pre-proxy with an accepted HTTP proxy through this method, so a changed exit code (5) would reach users.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/TransferProxySelection.cs:184:&& -Member TrySelectProxyBehindPreProxy -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-10-18: filed proposed.
- 2026-10-18: proposed -> accepted.
