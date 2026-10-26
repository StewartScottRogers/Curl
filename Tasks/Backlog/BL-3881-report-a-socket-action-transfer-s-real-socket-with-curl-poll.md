---
id: BL-3881
title: Report a socket-action transfer's real socket with CURL_POLL_OUT while it connects (CGR-034 C_65)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/TcpDialer.cs, Curl.Networking.UnitLibrary/ITcpDialer.cs, Curl.Engine.UnitLibrary/SocketActionGatedTcpDialer.cs, Curl.Engine.UnitLibrary/TransferSocketWait.cs, Curl.Networking.UnitTests/TcpDialerSocketCreatedTests.cs, Curl.Engine.UnitTests/SocketActionGatedTcpDialerConnectingTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3881 — Report a socket-action transfer's real socket with CURL_POLL_OUT while it connects (CGR-034 C_65)

## Goal

A transfer in a multi handle driven by `curl_multi_socket_action` gives the socket callback its real OS socket with `CURL_POLL_OUT` as soon as the socket exists, before the connect completes, so a refused connect gives curl 8.22.0's `T0 S=2 T-1 S=4` and result 7 (CGR-034 case `C_65`).

## Context

BL-3563 (ADR-0828) wires `ManagedMultiTransfer` to `TransferSocketWait`: the socket callback gets `TransferSocketWait.Socket`, which `SocketActionGatedTcpDialer` sets from the dialed connection's `SocketHandle` in `EndConnect`. The socket is known only once the dial returns, because `Curl.Networking`'s `TcpDialer` creates the `Socket` and connects it inside one call (`new Socket` in `TcpDialer.cs`). So while it connects, and for a connect that fails, the application is told no socket and the timer falls back to `ManagedMultiTransfer.RunningTransferCheckInterval`. Needed: a way for the dialer to publish the socket handle when it creates it (an optional hook on the dial that does nothing by default), which `SocketActionGatedTcpDialer` passes to the wait (a `TransferSocketWait` member that sets `Socket` while it is still connecting), so `Waiting` is `Out` on a known socket during the connect. BCL only, AOT-safe. BL-3563's wiring waits in its stash until this task is Done (it depends on this one); that stash adds `internal nint? Socket { get; private set; }` to `TransferSocketWait` and an `EndConnect(nint? socket = null)` that sets it. Add the same property here if it is not on the branch yet, so the stash applies with at most a trivial merge, and do not change `ManagedMultiTransfer.cs` (its `TellWaitedSocket` in the stash already reports any socket the wait holds).

## Acceptance criteria

- [ ] While a socket-action transfer's TCP connect is in progress, `TransferSocketWait.Socket` holds the socket's handle and `Waiting` is `CurlPoll.Out`; a unit test over a dialer that publishes a handle and holds the connect shows it.
- [ ] `TcpDialer` publishes each socket it creates through the hook before connecting it; a unit test shows it.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-26: Created.
