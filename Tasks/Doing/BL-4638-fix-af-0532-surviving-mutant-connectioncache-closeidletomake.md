---
id: BL-4638
title: Fix AF-0532: Surviving mutant: ConnectionCache.CloseIdleToMakeRoomAsync can be off by two on the connection limit without a test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-11-03
completed:
---
# BL-4638 — Fix AF-0532: Surviving mutant: ConnectionCache.CloseIdleToMakeRoomAsync can be off by two on the connection limit without a test failing

## Goal

The defect the audit office reported as AF-0532 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0532 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0532-surviving-mutant-connectioncache-closeidletomakero.md`.

Location: `Curl.Networking.UnitLibrary/ConnectionCache.cs:123`

Location: `Curl.Networking.UnitLibrary/ConnectionCache.cs:123`

Mutant 'Idle.Count + Leased.Count - Math.Max(limit - 1, 0)' -> 'Math.Max(limit + 1, 0)' survived (Curl.Networking.UnitTests green). The method enforces CURLMOPT_MAX_TOTAL_CONNECTIONS (CGR-150) by closing idle connections to make room. With the mutant, two more connections than the limit stay open before any idle one is closed, which changes how many sockets are open and which connections are reused or closed (the verbose closing lines).

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/ConnectionCache.cs:123:-1 -Member CloseIdleToMakeRoomAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
