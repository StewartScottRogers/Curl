---
id: BL-4768
title: Fix AF-0604: Surviving mutant: curl_share_cleanup can dispose the shared connections when it refuses and keep them when it succeeds without a test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4768 — Fix AF-0604: Surviving mutant: curl_share_cleanup can dispose the shared connections when it refuses and keep them when it succeeds without a test failing

## Goal

The defect the audit office reported as AF-0604 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0604 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0604-surviving-mutant-curl-share-cleanup-can-dispose-th.md`.

Location: `Curl.Engine.UnitLibrary/ManagedShareHandle.cs:102`

Location: `Curl.Engine.UnitLibrary/ManagedShareHandle.cs:102`

if (code == CurlShareCode.Ok) -> != survived (sample and targeted rerun): a cleanup refused as InUse then disposes the pool the attached transfers still use, and a successful one leaks it.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedShareHandle.cs:102:== -Member Cleanup -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Fix is tests only: `ManagedShareHandleTests` gains `Cleanup_RefusedInUse_KeepsTheSharedConnectionsOpenForTheAttachedTransfer` (a refused cleanup leaves the attached transfer's idle shared connection open and reused) and `Cleanup_Ok_ClosesTheSharedConnections` (a successful one closes it), over a `CloseCountingConnector` that counts closed connections. `ManagedShareHandle.cs` is unchanged, so no quality run was needed.
- A lane cannot run `Audit/Tools/Invoke-MutationTest.ps1` (audit paths are off limits), so the mutant was applied by hand at `ManagedShareHandle.cs:102` (`==` to `!=`): both new tests failed, so it is killed, and the original line was put back. The re-audit runs the tool itself.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-03: Doing -> Done. curl_share_cleanup's dispose-on-Ok mutant is killed by two ManagedShareHandleTests
