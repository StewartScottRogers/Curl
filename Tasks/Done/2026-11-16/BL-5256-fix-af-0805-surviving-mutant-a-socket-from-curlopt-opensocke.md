---
id: BL-5256
title: Fix AF-0805: Surviving mutant: a socket from CURLOPT_OPENSOCKETFUNCTION can be treated as the connection's own, changing when the close-socket callback runs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/SocketCallbackConnector.cs]
requirement: none
created: 2026-11-15
completed: 2026-11-15
---
# BL-5256 — Fix AF-0805: Surviving mutant: a socket from CURLOPT_OPENSOCKETFUNCTION can be treated as the connection's own, changing when the close-socket callback runs

## Goal

The defect the audit office reported as AF-0805 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0805 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitLibrary/SocketCallbackConnector.cs:249`

Location: `Curl.Engine.UnitLibrary/SocketCallbackConnector.cs:249`

The seed 770877 sample of Curl.Engine.UnitLibrary: '({ } open, _) => (open(CurlSocketType.IpConnection, address), false)' -> '..., true)' in SocketFor (the tool named the member OpenSocketAsync) survived. IsTheConnectionsOwnSocket then becomes true for the application's own socket. SocketCallbackConnection.DisposeAsync (lines 79-85) then calls the close-socket callback before the inner connection is disposed rather than after, so the order the application's callbacks see changes. No test checks the close order for a socket the open-socket callback supplied.

Reproduction: the audit office's mutation test changed `Curl.Engine.UnitLibrary/SocketCallbackConnector.cs line 249` as the evidence above describes (the mutant operator is `false`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Engine.UnitLibrary/SocketCallbackConnector.cs line 249`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mutant (line 249 false -> true) applied by hand: the new test ConnectAsync_OpenSocketCallbackSuppliesTheSocket_ClosesTheSocketAfterTheInnerConnectionIsDisposed failed; reverted, all fast tests pass. Added FakeConnection.OnDispose in Curl.Engine.UnitTests/SocketCallbackConnectorTests.cs (touches widened to that test file).

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing. Criteria at claim: d56c40e9 9742157d.
- 2026-11-15: Doing -> Done. Test pins close-socket order for open-socket-supplied sockets, killing the AF-0805 mutant
