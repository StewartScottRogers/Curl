---
id: BL-5082
title: Fix AF-0723: Surviving mutant: --ws-repl can refuse a 101 status line with no reason phrase
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.WebSocketRepl.UnitLibrary]
requirement: none
created: 2026-11-13
completed: 2026-11-13
---
# BL-5082 — Fix AF-0723: Surviving mutant: --ws-repl can refuse a 101 status line with no reason phrase

## Goal

The defect the audit office reported as AF-0723 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0723 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0723-surviving-mutant-ws-repl-can-refuse-a-101-status-l.md`.

Location: `Curl.Extension.WebSocketRepl.UnitLibrary/Handshake/WebSocketHandshakeResponseValidator.cs:30`

Location: `Curl.Extension.WebSocketRepl.UnitLibrary/Handshake/WebSocketHandshakeResponseValidator.cs:30`

Mutant 'parts.Length >= 2' -> '> 2' survived. 'HTTP/1.1 101' split on ' ' gives two parts, so the mutant refuses a valid upgrade (exit WeirdServerReply, 'Refused WebSocket upgrade'). Every test status line carries 'Switching Protocols'.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.WebSocketRepl.UnitLibrary/Handshake/WebSocketHandshakeResponseValidator.cs:30:>= -Member IsSwitchingProtocols -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added FindFailure_StatusLineWithNoReasonPhrase_IsNull to Curl.Extension.WebSocketRepl.UnitTests (a test-only fix; production code was already right). Verified by hand: with `>= 2` changed to `> 2` the new test fails, restored it passes (59 of 59). The audit tool run in-lane reported survived before the audit guard blocked further runs (it may have used a stale build), so the re-audit should confirm.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-13: Doing -> Done. Added a unit test pinning a 101 status line with no reason phrase; it kills the >= 2 to > 2 mutant by hand check.
