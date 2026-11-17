---
id: BL-5255
title: Fix AF-0804: Surviving mutant: curl_multi_socket_action can signal the wrong events to a waiting transfer
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedMultiTransfer.Perform.cs]
requirement: none
created: 2026-11-15
completed: 2026-11-15
---
# BL-5255 — Fix AF-0804: Surviving mutant: curl_multi_socket_action can signal the wrong events to a waiting transfer

## Goal

The defect the audit office reported as AF-0804 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0804 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitLibrary/ManagedMultiTransfer.Perform.cs:60`

Location: `Curl.Engine.UnitLibrary/ManagedMultiTransfer.Perform.cs:60`

The seed 770877 sample of Curl.Engine.UnitLibrary (one of the three largest): 'Signal(events == CurlSocketSelect.None ? SelectOf(entry.SocketWait.Waiting) : events)' -> 'events != CurlSocketSelect.None ? ...' in SocketActionAsync survived. The tool named the member '-' because the tuple return type hid the declaration. With the mutant, an application passing CURL_CSELECT_IN/OUT has the transfer woken with what it waits for, not what happened. One passing 0 (check state) has it signalled with no events, which can leave it waiting. No test drives socket_action with both a zero and a non-zero event mask and checks what the transfer saw.

Reproduction: the audit office's mutation test changed `Curl.Engine.UnitLibrary/ManagedMultiTransfer.Perform.cs line 60` as the evidence above describes (the mutant operator is `==`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Engine.UnitLibrary/ManagedMultiTransfer.Perform.cs line 60`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by `SocketActionAsync_NoEventsOnTheSocketOfAReadWait_WakesTheWaitingRead` (ManagedMultiTransferTests.SocketAction.cs): with the mutant it fails, reverted all 3051 Engine fast tests pass. CI platform-neutral (no OS-specific code).

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing. Criteria at claim: 796777ef 9742157d.
- 2026-11-15: Doing -> Done. Added test killing the socket_action events mutant
