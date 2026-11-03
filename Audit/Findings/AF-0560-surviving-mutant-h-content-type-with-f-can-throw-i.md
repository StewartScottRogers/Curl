---
id: AF-0560
title: Surviving mutant: -H "Content-Type" with -F can throw IndexOutOfRangeException without a test failing
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Engine.UnitLibrary/MultipartFormPartMapping.cs:RequestContentTypeOf-gt:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/MultipartFormPartMapping.cs:66:>
task: none
tasks:
found: 2026-11-03
found-at: ef78cb0569375bf8475eb1a2da931cfd224d52f4
scorecard: 2026-11-03_0025.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0560 - Surviving mutant: -H "Content-Type" with -F can throw IndexOutOfRangeException without a test failing

## Summary

High finding from the quality auditor at `Curl.Engine.UnitLibrary/MultipartFormPartMapping.cs:66`: Surviving mutant: -H "Content-Type" with -F can throw IndexOutOfRangeException without a test failing. Reported by an auditor flagged unreliable in 2026-11-03_0025.md.

## Evidence

Location: `Curl.Engine.UnitLibrary/MultipartFormPartMapping.cs:66`

Mutant 'header.Length > ContentTypeName.Length' -> '>=' survived. With it, a header of exactly 'Content-Type' reaches 'header[ContentTypeName.Length]', one past its end, and the form build throws instead of ignoring the header. No test passes an -H value equal to the bare name.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/MultipartFormPartMapping.cs:66:> -Member RequestContentTypeOf -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-03: filed proposed.
