---
id: BL-4815
title: Fix AF-0636: Surviving mutant: the multi handle's wait can count finished or never-started transfers as running, and no test fails
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-04
completed:
---
# BL-4815 — Fix AF-0636: Surviving mutant: the multi handle's wait can count finished or never-started transfers as running, and no test fails

## Goal

The defect the audit office reported as AF-0636 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0636 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0636-surviving-mutant-the-multi-handle-s-wait-can-count.md`.

Location: `Curl.Engine.UnitLibrary/ManagedMultiTransfer.WaitSockets.cs:142`

Location: `Curl.Engine.UnitLibrary/ManagedMultiTransfer.WaitSockets.cs:142`

Invoke-MutationTest -Library Curl.Engine.UnitLibrary -MaxMutants 40 -Seed 221035: survived, 'entries.FindAll(entry => !entry.Done && entry.Perform is not null)' -> '||'. Done transfers that have a perform, and pending ones not yet started, are counted among the running ones a curl_multi_wait waits on, which changes the wait's sockets and when it returns. No test waits with a finished or pending transfer beside a running one.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedMultiTransfer.WaitSockets.cs:142:&& -Member WaitedSockets -ExcludeBaselineFailures -TimeoutSeconds 600
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
