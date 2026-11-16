---
id: AF-0817
title: Surviving mutant: an ODoH response whose status line has no reason phrase can be refused, failing the resolve
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-16_1333.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Extension.ObliviousDoh.UnitLibrary/Http/ObliviousDohHttpResponseParser.cs:StatusCodeOf-ge:surviving-mutant
reproduction: mutation Curl.Extension.ObliviousDoh.UnitLibrary/Http/ObliviousDohHttpResponseParser.cs:32:>=
task: BL-5271
tasks: BL-5271
found: 2026-11-16
found-at: 74f3f45c47e86300b2b262dc1dacd43b508b09aa
scorecard: 2026-11-16_0514.md
duplicate-of:
closed: 2026-11-16
closed-how: mechanical
closed-by: 2026-11-16_1333.md
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

- 2026-11-16 | 2026-11-16_1333.md | reproduces: no | Ran the -Site command: resolvedLine 32 ('parts.Length >= 2' to '> 2'), outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-16: filed proposed.
- 2026-11-16: accepted -> closed. Re-audit 2026-11-16_1333.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
