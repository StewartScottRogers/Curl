---
id: BL-5257
title: Fix AF-0806: Surviving mutant: the dialer can take ownership of an application-supplied socket and close it itself
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/TcpDialer.cs, Curl.Networking.UnitTests/SocketOpenerTcpDialerTests.cs]
requirement: none
created: 2026-11-15
completed: 2026-11-15
---
# BL-5257 — Fix AF-0806: Surviving mutant: the dialer can take ownership of an application-supplied socket and close it itself

## Goal

The defect the audit office reported as AF-0806 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0806 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Networking.UnitLibrary/TcpDialer.cs:346`

Location: `Curl.Networking.UnitLibrary/TcpDialer.cs:346`

The seed 770877 sample of Curl.Networking.UnitLibrary (one of the three largest): 'new Socket(new SafeSocketHandle(handle, ownsHandle: false))' -> 'ownsHandle: true' in DialSuppliedSocketAsync survived. The managed Socket then closes the handle the application supplied (CURLOPT_OPENSOCKETFUNCTION) when it is disposed. That handle is the application's to close through its close-socket callback, so the result is a double close or a handle closed under it. No test checks that a supplied handle is still open after the connection is disposed.

Reproduction: the audit office's mutation test changed `Curl.Networking.UnitLibrary/TcpDialer.cs line 346` as the evidence above describes (the mutant operator is `false`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Networking.UnitLibrary/TcpDialer.cs line 346`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Killed by new test `SocketOpenerTcpDialerTests.DialAsync_WithASuppliedSocket_LeavesTheHandleOpenAfterTheConnectionIsDisposed` (checked by hand: ownsHandle true fails it, false passes all 10 in the class; Networking fast tests 4159 pass). No production change. touches widened to the test file.

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing. Criteria at claim: ffdac368 9742157d.
- 2026-11-15: Doing -> Done. Test now kills the ownsHandle mutant in DialSuppliedSocketAsync
