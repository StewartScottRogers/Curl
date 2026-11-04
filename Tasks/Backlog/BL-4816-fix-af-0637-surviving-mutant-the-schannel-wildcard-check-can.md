---
id: BL-4816
title: Fix AF-0637: Surviving mutant: the Schannel wildcard check can refuse a host whose first label is one character, and no test fails
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-11-04
completed:
---
# BL-4816 — Fix AF-0637: Surviving mutant: the Schannel wildcard check can refuse a host whose first label is one character, and no test fails

## Goal

The defect the audit office reported as AF-0637 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0637 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0637-surviving-mutant-the-schannel-wildcard-check-can-r.md`.

Location: `Curl.Networking.UnitLibrary/SchannelCommonNameCheck.cs:67`

Location: `Curl.Networking.UnitLibrary/SchannelCommonNameCheck.cs:67`

Invoke-MutationTest -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 221035: survived, 'hostLabelEnd < 1' -> 'hostLabelEnd <= 1' in WildcardMatchesHost. A host such as a.hello.com (first label one character) no longer matches a *.hello.com certificate, so the Schannel build fails a valid certificate with exit 60. The tests cover '.hello.com' (index 0) and longer labels, never a one-character label.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/SchannelCommonNameCheck.cs:67:< -Member WildcardMatchesHost -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-04: Created.
