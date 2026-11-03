---
id: AF-0563
title: Surviving mutant: Lib677.Advance can keep its send position after a zero-length send without a test failing
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib677.cs:Advance-gt:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib677.cs:188:>
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
# AF-0563 - Surviving mutant: Lib677.Advance can keep its send position after a zero-length send without a test failing

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib677.cs:188`: Surviving mutant: Lib677.Advance can keep its send position after a zero-length send without a test failing. Reported by an auditor flagged unreliable in 2026-11-03_0025.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib677.cs:188`

Mutant 'position = sent || length > 0 ? position + length : 0;' -> 'length >= 0' survived. A zero-length curl_easy_send while the command is half sent no longer restarts the command from 0, which changes the bytes the port sends; no test drives a zero-length send.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib677.cs:188:> -Member Advance -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted.
