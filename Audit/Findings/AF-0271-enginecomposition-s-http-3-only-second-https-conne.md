---
id: AF-0271
title: EngineComposition's HTTP/3-only second HTTPS-connect attempt can apply to every version with no Engine test failing
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Engine.UnitLibrary/EngineComposition.cs:ConnectToMappings-eq:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/EngineComposition.cs:1046:==
task: none
tasks:
found: 2026-10-18
found-at: e17dfce54cf6f2d08fc7c4abe0655128f237ff3b
scorecard: 2026-10-18_1905.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0271 - EngineComposition's HTTP/3-only second HTTPS-connect attempt can apply to every version with no Engine test failing

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/EngineComposition.cs:1046`: EngineComposition's HTTP/3-only second HTTPS-connect attempt can apply to every version with no Engine test failing. Reported by an auditor flagged unreliable in 2026-10-18_1905.md.

## Evidence

Location: `Curl.Engine.UnitLibrary/EngineComposition.cs:1046`

Mutant HttpsConnectSecondAttemptVersion = http.HttpVersion == HttpVersion.Http3 ? "h2" : null -> != survived in Curl.Engine.UnitTests (sampled run, seed 0). With it every non-HTTP/3 HTTPS connect is given an h2 second attempt for the [HTTPS-CONNECT] filter, and HTTP/3 gets none. That is a connect decision whose trace lines reach -v output. Only Curl.Console.UnitTests (CurlCommandRunnerHttpsConnectTraceTests:156) pins the value; the library's own twin does not.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/EngineComposition.cs:1046:== -Member ConnectToMappings -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-10-18: filed proposed.
- 2026-10-19: proposed -> accepted.
