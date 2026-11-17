---
id: AF-0848
title: Surviving mutant: a --resolve entry with an empty host is kept instead of ignored
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Networking.UnitLibrary/ResolveOverrides.cs:TryParseAddition-gt:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/ResolveOverrides.cs:150:>
task: none
tasks:
found: 2026-11-17
found-at: 10c240a4859c7868ec16b722cde6234f80cc8fcb
scorecard: 2026-11-17_0430.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0848 - Surviving mutant: a --resolve entry with an empty host is kept instead of ignored

## Summary

High finding from the quality auditor at `Curl.Networking.UnitLibrary/ResolveOverrides.cs:150`: Surviving mutant: a --resolve entry with an empty host is kept instead of ignored.

## Evidence

Location: `Curl.Networking.UnitLibrary/ResolveOverrides.cs:150`

if (host.Length > 0) -> >= 0 survived. The code's own comment says ':80:127.0.0.1' is ignored (measured). Under the mutant the entry is added, so LoadResolveEntries writes an 'Added :80:127.0.0.1 to DNS cache' -v line curl does not write (stderr bytes).

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/ResolveOverrides.cs:150:> -Member TryParseAddition -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-17: filed proposed.
