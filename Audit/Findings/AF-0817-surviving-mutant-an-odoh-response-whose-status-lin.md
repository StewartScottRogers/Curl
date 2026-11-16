---
id: AF-0817
title: Surviving mutant: an ODoH response whose status line has no reason phrase can be refused, failing the resolve
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Extension.ObliviousDoh.UnitLibrary/Http/ObliviousDohHttpResponseParser.cs:StatusCodeOf-ge:surviving-mutant
reproduction: mutation Curl.Extension.ObliviousDoh.UnitLibrary/Http/ObliviousDohHttpResponseParser.cs:32:>=
task: none
tasks:
found: 2026-11-16
found-at: 74f3f45c47e86300b2b262dc1dacd43b508b09aa
scorecard: 2026-11-16_0514.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0817 - Surviving mutant: an ODoH response whose status line has no reason phrase can be refused, failing the resolve

## Summary

High finding from the quality auditor at `Curl.Extension.ObliviousDoh.UnitLibrary/Http/ObliviousDohHttpResponseParser.cs:32`: Surviving mutant: an ODoH response whose status line has no reason phrase can be refused, failing the resolve.

## Evidence

Location: `Curl.Extension.ObliviousDoh.UnitLibrary/Http/ObliviousDohHttpResponseParser.cs:32`

`parts.Length >= 2` changed to `parts.Length > 2` survived. 'HTTP/1.1 200' (no reason phrase, valid under RFC 9112) then parses to no status, so the response is null, the resolver gets no addresses and the transfer ends with a resolve failure instead of connecting. Every test response carries a reason phrase.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.ObliviousDoh.UnitLibrary/Http/ObliviousDohHttpResponseParser.cs:32:>= -Member StatusCodeOf -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-16: filed proposed.
