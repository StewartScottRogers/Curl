---
id: AF-0998
title: Surviving mutant: the lib1560 port can report the zoneid part as UNKNOWN (> -> >=)
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib1560Checks.cs:SetPart-gt:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib1560Checks.cs:134:>
task: BL-5992
tasks: BL-5992
found: 2026-11-25
found-at: eb33fe3c5ab162945003079cf4f807e5608dde01
scorecard: 2026-11-25_1306.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0998 - Surviving mutant: the lib1560 port can report the zoneid part as UNKNOWN (> -> >=)

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib1560Checks.cs:134`: Surviving mutant: the lib1560 port can report the zoneid part as UNKNOWN (> -> >=).

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1560Checks.cs:134`

Mutant 'if (part > CurlUrlPart.ZoneId)' -> 'if (part >= CurlUrlPart.ZoneId)' survived. Lib1560Tables.SetParts.cs:40 sets 'zoneid=NULL,' on https://[::1%25fake]:1234/, so with the mutant the port writes a spurious "UNKNOWN part 'zoneid'" line that upstream lib1560 never writes. No test checks the port's error output for a zoneid set.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib1560Checks.cs:134:> -Member SetPart -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-25: filed proposed.
