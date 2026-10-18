---
id: BL-2761
title: Fix AF-0250: TrySelectProxyBehindPreProxy's && can become || with no test failing, refusing every HTTP proxy behind a pre-proxy with exit 5
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-10-18
completed:
---
# BL-2761 — Fix AF-0250: TrySelectProxyBehindPreProxy's && can become || with no test failing, refusing every HTTP proxy behind a pre-proxy with exit 5

## Goal

The defect the audit office reported as AF-0250 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0250 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0250-tryselectproxybehindpreproxy-s-can-become-with-no.md`.

Location: `Curl.Engine.UnitLibrary/TransferProxySelection.cs:184`

Location: `Curl.Engine.UnitLibrary/TransferProxySelection.cs:184`

Engine sample, seed 0: survived. Original `if (selected is not null && !IsHttpKind(selected.Kind))`, mutated `||`. With the mutant, any selected proxy, including an HTTP one, fails with CouldntResolveProxy "Having a SOCKS pre-proxy and proxy is not supported". A null selection would throw a NullReferenceException. No test runs a pre-proxy with an accepted HTTP proxy through this method, so a changed exit code (5) would reach users.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/TransferProxySelection.cs:184:&& -Member TrySelectProxyBehindPreProxy -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-18: Created.
