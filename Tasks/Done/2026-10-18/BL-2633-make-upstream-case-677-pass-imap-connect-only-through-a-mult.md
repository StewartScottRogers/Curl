---
id: BL-2633
title: Make upstream case 677 pass: IMAP CONNECT_ONLY through a multi handle then curl_easy_send
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2623]
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Api.UnitLibrary/Info/TransferInfo.cs, Documentation/Planning/Decisions/ADR-0549-easy-handle-info-build-constants-per-platform-and-no-sockets.md]
requirement: none
created: 2026-10-17
completed: 2026-10-18
---
# BL-2633 — Make upstream case 677 pass: IMAP CONNECT_ONLY through a multi handle then curl_easy_send

## Goal

Upstream case 677 passes through the runner.

## Context

Found by BL-2535, which ported the libtest program and unit tested it over a fake engine; the port matches upstream's C, so the difference is in the engine or Libcurl. In the first pass find the project at fault and add it to `touches` before any edit. test677 (lib677: IMAP `CURLOPT_CONNECT_ONLY` on a multi handle, `CURLINFO_ACTIVESOCKET` after the done message, then `curl_easy_send` of `A1 IDLE` when `curl_multi_wait` reports the socket writable): `differs at byte 0: expected "A001 CAPABILITY\r\n", got the end` - the same cause as BL-2623 (connect-only through a multi handle never connects); after it, check `curl_multi_wait` fills an extra socket's `revents`.

## Acceptance criteria

- [x] Each named case that passes through the runner on Windows is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; one that still fails is filed with the board script with its differing lines.
- [x] The fix adds a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Cause: with BL-2623 the IMAP connect-only perform connects, but CURLINFO_ACTIVESOCKET was always -1 (ADR-0549), so lib677 ended at the done message without sending A1 IDLE (protocol stopped after A001 CAPABILITY, stdout empty).
- Fix (Curl.Engine.UnitLibrary, ADR-0686): ConnectOnlyConnection numbers each kept connection from 0x60000000; ManagedEasyTransfer.Info answers it as ActiveSocket and LastSocket while open and connect-only; ManagedMultiTransfer.PollWaitSocket answers that number from ConnectOnlyConnection.ReadyEvents (POLLOUT while no send runs, POLLIN once bytes wait) for a transfer in the handle, else asks IWaitSocketPoller. Chose an engine number over the real Socket handle: the harness, TLS and proxied connections have no usable descriptor.
- Touches widened to Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Api.UnitLibrary/Info/TransferInfo.cs (doc comments said always -1) and ADR-0549 (amended); no other task was in Doing on origin/work/dark-factory.
- Case 677 is in-memory IMAP only, no paths or OS text, so platform neutral. Focused tests: ConnectOnlyConnectionTests (Socket, LiveSocket, ReadyEvents), ManagedEasyTransferTests.ConnectOnly (Info sockets, multi wait readiness), ManagedMultiTransferTests.Timers (poller fallback). Measure-CodeQuality for Curl.Engine.UnitLibrary: 100% line, 100% branch, 0 failing.
- Not done here: CURLOPT_CONNECT_ONLY 2 (WebSocket) connections still answer -1 for ACTIVESOCKET; no current upstream case needs it.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 92cbe6ef c59fcab3 3d836d5e.
- 2026-10-18: Doing -> Done. Upstream case 677 passes: connect-only connections answer an engine socket for CURLINFO_ACTIVESOCKET that curl_multi_wait polls
