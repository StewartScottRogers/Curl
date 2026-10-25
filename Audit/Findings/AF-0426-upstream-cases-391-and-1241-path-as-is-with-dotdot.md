---
id: AF-0426
title: Upstream cases 391 and 1241 (--path-as-is with dotdots) are on the passing list but fail unmutated: Curl sends no request
auditor: quality
severity: High
status: rejected
reason: Stewart rejected it on 2026-10-25: it is planted defect PD-303 (the --path-as-is row removed from CommandLineOptionTable.cs, run 20261024-210628's manifest), reported by the quality auditor from another file; the audited commit passes 391 and 1241.
key: quality:Curl.Conformance.UnitTests/UpstreamConformanceTests.cs:UpstreamCase_RunThroughCurl_HoldsTheRatchet:failing-test
reproduction: none
task: none
tasks:
found: 2026-10-24
found-at: 456765a13655caedad49326c988f9e4eec86da8b
scorecard: 2026-10-24_2106.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0426 - Upstream cases 391 and 1241 (--path-as-is with dotdots) are on the passing list but fail unmutated: Curl sends no request

## Summary

High finding from the quality auditor at `Curl.Conformance.UnitTests/UpstreamConformanceTests.cs:129`: Upstream cases 391 and 1241 (--path-as-is with dotdots) are on the passing list but fail unmutated: Curl sends no request.

## Evidence

Location: `Curl.Conformance.UnitTests/UpstreamConformanceTests.cs:129`

The Curl.Conformance.UnitLibrary mutation baseline had to exclude UpstreamCase_RunThroughCurl_HoldsTheRatchet. Running it alone: 'Failed: 2, Passed: 1984, Skipped: 86'. test391 [http] '--path-as-is with redirect, keeping dotdots': 'test391 is on PassingUpstreamCases.txt and now fails: <verify><protocol> differs at byte 0 (line 1): expected "GET /../../391 HTTP/1.1\r\n", got the end'. test1241 [http] 'HTTP _without_ dotdot removal': expected "GET http://test.remote.haxx.se.1241:8990/../../hej/but/who/../1241?stupid=me/../1241 HTTP/1.1\r\n", got the end. No request bytes reach the server for a --path-as-is URL with .. segments, and no unit test outside the ratchet caught it.

## Reproduction

Run from the repository root:

```powershell
dotnet test Curl.Conformance.UnitTests --filter "FullyQualifiedName~UpstreamCase_RunThroughCurl_HoldsTheRatchet&TestCategory!=Integration"
```

- Expected: No failures: every case on PassingUpstreamCases.txt passes.
- Actual: Failed: 2 (test391, test1241), each with <verify><protocol> 'got the end' at byte 0.

## Re-audits

## Log

- 2026-10-24: filed proposed.
- 2026-10-25: proposed -> rejected.
