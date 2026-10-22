---
id: AF-0325
title: ADR-0747 says case 1478 is skipped because no managen interpretation is registered; one is now registered
auditor: truthfulness
severity: Low
status: proposed
reason:
key: truthfulness:Documentation/Planning/Decisions/ADR-0747-upstream-case-1478-stays-off-the-ratchet.md:Case1478Skip:stale-adr
reproduction: none
task: none
tasks:
found: 2026-10-22
found-at: 167e765b2a009a4887fc971075283ef9d24a73a1
scorecard: 2026-10-22_0130.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0325 - ADR-0747 says case 1478 is skipped because no managen interpretation is registered; one is now registered

## Summary

Low finding from the truthfulness auditor at `Documentation/Planning/Decisions/ADR-0747-upstream-case-1478-stays-off-the-ratchet.md:28`: ADR-0747 says case 1478 is skipped because no managen interpretation is registered; one is now registered. Reported by an auditor flagged unreliable in 2026-10-22_0130.md.

## Evidence

Location: `Documentation/Planning/Decisions/ADR-0747-upstream-case-1478-stays-off-the-ratchet.md:28`

ADR-0747:28-30: 'The harness keeps skipping it with `the harness does not run a perl command`, since no interpretation of `managen` is registered.' Curl.Conformance.UnitTests/UpstreamPerlCommandScripts.Managen.cs:7-9 registers UpstreamManagenScript under the name 'managen' in a [ModuleInitializer]. UpstreamCaseScreening.UnrunCommandType (Curl.Conformance.UnitLibrary/UpstreamCaseScreening.cs:380) skips a perl command only when its script is not registered. test1478's command is '%SRCDIR/../scripts/managen ... listhelp ...' (UpstreamTestData/test1478.curltest:18), so it is no longer skipped with that message. It now runs ManualPageGenerator, which carries out only mainpage and ascii. The decision itself still holds: 1478 is absent from PassingUpstreamCases.txt.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Documentation/Planning/Decisions/ADR-0747-*.md -Pattern 'no interpretation of'; Select-String -Path Curl.Conformance.UnitTests/UpstreamPerlCommandScripts.Managen.cs -Pattern 'ModuleInitializer|Register\('; Select-String -Path Curl.Conformance.UnitTests/UpstreamTestData/test1478.curltest -Pattern 'scripts/managen'
```

- Expected: No managen registration (the ADR's stated reason holds), or the ADR describes how 1478 is handled now.
- Actual: ADR-0747:29 says no managen interpretation is registered; UpstreamPerlCommandScripts.Managen.cs:7-9 registers it at module load, and test1478.curltest:18 runs scripts/managen.

## Re-audits

## Log

- 2026-10-22: filed proposed.
