---
id: BL-4794
title: Fix AF-0621: Surviving mutant: CurlSocketCloser's default can mark every socket as the connection's own without a test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-03
completed:
---
# BL-4794 — Fix AF-0621: Surviving mutant: CurlSocketCloser's default can mark every socket as the connection's own without a test failing

## Goal

The defect the audit office reported as AF-0621 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0621 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0621-surviving-mutant-curlsocketcloser-s-default-can-ma.md`.

Location: `Curl.Engine.UnitLibrary/CurlSocketCloser.cs:19`

Location: `Curl.Engine.UnitLibrary/CurlSocketCloser.cs:19`

Mutant 'bool isTheConnectionsOwnSocket = false' -> '= true' survived. Two callers rely on the default: SocketCallbackConnectionListener.cs:63 (an accepted socket with the close-socket callback) and SocketCallbackConnector.cs:116. With the mutant, IsTheConnectionsOwnSocket is true for them, so the close-socket callback runs before the connection closes rather than after (CGR-129). That changes the callback order libcurl callers observe; no test checks it for these two paths.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/CurlSocketCloser.cs:19:false -Member CurlSocketCloser -ExcludeBaselineFailures -TimeoutSeconds 600
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
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
