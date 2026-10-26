---
id: BL-3833
title: Networking: a per-attempt TCP socket-open hook on ConnectTarget (refuse, supply a socket, closed notice) for CGR-036
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary/ConnectTarget.cs, Curl.Protocol.Abstractions.UnitLibrary/ITcpSocketOpener.cs, Curl.Networking.UnitLibrary/TcpConnector.cs, Curl.Networking.UnitLibrary/SocketOpenerTcpDialer.cs, Curl.Networking.UnitTests/SocketOpenerTcpDialerTests.cs, Curl.Protocol.Abstractions.UnitLibrary/TcpSocketOpenDecision.cs, Curl.Networking.UnitLibrary/TcpDialer.cs, Curl.Networking.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3833 — Networking: a per-attempt TCP socket-open hook on ConnectTarget (refuse, supply a socket, closed notice) for CGR-036

## Goal

A connect can carry a per-attempt socket-open hook that `TcpConnector`'s address race consults before it opens each TCP socket, so libcurl's `CURLOPT_OPENSOCKETFUNCTION` can run before the connect (as curl 8.22.0's `cf_socket_open` does) instead of after it (BL-3565, CGR-036).

## Context

CGR-036 (`Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`): `Curl.Engine.UnitLibrary/SocketCallbackConnector.cs` lets the inner connector connect, then calls the open-socket callback with the peer address. So a refusal (`CURL_SOCKET_BAD`) comes after the server has accepted a connection, a socket the application supplies is ignored, and a failed connect calls neither the open nor the close callback. Curl.Engine cannot fix that alone: the address race (`Curl.Networking.UnitLibrary/AddressFamilyRace.cs`, built at `TcpConnector.cs` ~2408 with `BindingDialer(target.Events)`) opens the sockets, and nothing per transfer reaches it before a dial.

Suggested shape (Decided by Claude under Stewart's delegation when BL-3565 was split; adjust if the code shows a simpler seam, and record it in an ADR):
- `Curl.Protocol.Abstractions`: `ITcpSocketOpener` with one method taking the remote `IPEndPoint` and giving a decision: `Proceed` (the dialer opens its own socket), `Refuse` (the attempt fails as a socket the OS would not open does, so with every address refused the connect ends exit 7 and no TCP connection is made), or `Supplied(nint handle, bool alreadyConnected)` (adopt the OS socket handle the application made: `new Socket(new SafeSocketHandle(handle, ownsHandle: false))`, and connect it unless already connected); plus a notice that the attempt's socket closed when its connect fails. `ConnectTarget.SocketOpener { get; init; }`, null by default.
- `Curl.Networking`: `SocketOpenerTcpDialer`, an `ITcpDialer` decorator applied in `TcpConnector` when `target.SocketOpener` is set, so `AddressFamilyRace` needs no change.
- BL-3565 then switches `SocketCallbackConnector` to set the hook (open-socket and socket-option callbacks before the connect, close-socket callback on a failed attempt) instead of calling them afterwards.

## Acceptance criteria

- [x] With a `ConnectTarget.SocketOpener` that refuses every address, `TcpConnector` fails the connect with exit 7 and a loopback listener accepts no connection (unit test).
- [x] With an opener that supplies an already-connected socket to a second loopback listener, the connection's bytes go to that listener, not to the target's address (unit test).
- [x] With an opener on a connect to a closed port, the opener is asked once with the address and told once that the socket closed, and the connect ends exit 7 (unit test).
- [x] A connect with no opener behaves as before (the existing Networking tests pass).
- [x] `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary,Curl.Protocol.Abstractions.UnitLibrary` reports no member outside a gate for the changed code.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Filed by BL-3565's first claim (lane 7), which found the fix needs this seam outside Curl.Engine. Proxies' TCP connects go through the same race, as curl calls OPENSOCKETFUNCTION for the proxy connection too.
- Delivered (ADR-0818): `ITcpSocketOpener` and `TcpSocketOpenDecision` (Proceed, Refuse, Supplied(handle, alreadyConnected)) in Abstractions, `ConnectTarget.SocketOpener`, and `SocketOpenerTcpDialer` wrapping the race's dialer in `TcpConnector.AttemptDialer` when the target has an opener; `AddressFamilyRace` is unchanged.
- Touches widened (no task in Doing on origin/work/dark-factory named them): `TcpSocketOpenDecision.cs` (one public type per file), `TcpDialer.cs` (the supplied-handle adoption `DialSuppliedSocketAsync` lives there so `TcpDialer` stays the one type constructing a TCP `Socket`, per the project's CLAUDE.md), and `Curl.Networking.UnitLibrary/CLAUDE.md` (one paragraph on the hook).
- Decisions: a refused attempt throws `SocketException(SocketError.SocketError)` before any socket, so the race reports it as a failed dial and ends exit 7; `SocketClosed` is told only for an opened attempt that failed or was abandoned, not when a successful connection is later disposed (BL-3565's close-socket callback handles that); the supplied handle is adopted with `ownsHandle: false`.
- Criterion 2 is checked without sending bytes, so it stays a unit test (root rules: a loopback socket connected without a byte sent): the connection's local end is the remote end the second listener accepted, and the target's listener has nothing waiting.
- Measured: the new tests 9/9; dotnet build clean; fast tests green in all 46 test projects; `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary,Curl.Protocol.Abstractions.UnitLibrary`: 0 failing members. The tests use only loopback IPv4 and no OS error text, so they are platform-neutral for CI on Linux and macOS.

## Log

- 2026-10-26: Created.
- 2026-10-26: Backlog -> Doing. Criteria at claim: a4e2fcec 5f3b82ff 8d39f34c ece5cec8 67ad3778 779790c0.
- 2026-10-26: Doing -> Done. A ConnectTarget.SocketOpener is asked before each TCP attempt to proceed, refuse (exit 7, no connection) or supply a socket, and told when a failed attempt's socket closed
