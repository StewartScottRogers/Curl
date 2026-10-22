---
id: BL-3078
title: Fix AF-0325: ADR-0747 says case 1478 is skipped because no managen interpretation is registered; one is now registered
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation]
requirement: none
created: 2026-10-22
completed:
---
# BL-3078 — Fix AF-0325: ADR-0747 says case 1478 is skipped because no managen interpretation is registered; one is now registered

## Goal

The defect the audit office reported as AF-0325 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0325 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0325-adr-0747-says-case-1478-is-skipped-because-no-mana.md`.

Location: `Documentation/Planning/Decisions/ADR-0747-upstream-case-1478-stays-off-the-ratchet.md:28`

Location: `Documentation/Planning/Decisions/ADR-0747-upstream-case-1478-stays-off-the-ratchet.md:28`

ADR-0747:28-30: 'The harness keeps skipping it with `the harness does not run a perl command`, since no interpretation of `managen` is registered.' Curl.Conformance.UnitTests/UpstreamPerlCommandScripts.Managen.cs:7-9 registers UpstreamManagenScript under the name 'managen' in a [ModuleInitializer]. UpstreamCaseScreening.UnrunCommandType (Curl.Conformance.UnitLibrary/UpstreamCaseScreening.cs:380) skips a perl command only when its script is not registered. test1478's command is '%SRCDIR/../scripts/managen ... listhelp ...' (UpstreamTestData/test1478.curltest:18), so it is no longer skipped with that message. It now runs ManualPageGenerator, which carries out only mainpage and ascii. The decision itself still holds: 1478 is absent from PassingUpstreamCases.txt.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Documentation/Planning/Decisions/ADR-0747-*.md -Pattern 'no interpretation of'; Select-String -Path Curl.Conformance.UnitTests/UpstreamPerlCommandScripts.Managen.cs -Pattern 'ModuleInitializer|Register\('; Select-String -Path Curl.Conformance.UnitTests/UpstreamTestData/test1478.curltest -Pattern 'scripts/managen'
```

- Expected: No managen registration (the ADR's stated reason holds), or the ADR describes how 1478 is handled now.
- Actual: ADR-0747:29 says no managen interpretation is registered; UpstreamPerlCommandScripts.Managen.cs:7-9 registers it at module load, and test1478.curltest:18 runs scripts/managen.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
