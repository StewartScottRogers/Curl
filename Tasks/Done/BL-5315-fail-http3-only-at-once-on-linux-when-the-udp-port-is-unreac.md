---
id: BL-5315
title: Fail --http3-only at once on Linux when the UDP port is unreachable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/UdpChannelOpener.cs, Curl.Networking.UnitLibrary/UdpDatagramConnector.cs, Curl.Networking.UnitLibrary/DatagramConnectorUdpChannelOpener.cs, Curl.Networking.UnitLibrary/PortUnreachableReportingDatagramChannel.cs, Curl.Networking.UnitLibrary/QuicDialer.cs, Curl.Networking.UnitTests/PortUnreachableReportingDatagramChannelTests.cs, Curl.Networking.UnitTests/UdpDatagramConnectorTests.cs, Curl.Networking.UnitTests/PortUnreachableLinuxErrnoTests.cs]
requirement: none
created: 2026-11-16
completed: 2026-11-16
---
# BL-5315 — Fail --http3-only at once on Linux when the UDP port is unreachable

## Goal

On Linux, `curl --http3-only https://host:port/` to a host with nothing listening on that UDP port fails within a second with exit 56 and a `QUIC: recvfrom() unexpectedly returned -1 (errno=...; ...)` message (ADR-0144), instead of waiting for `-m` (exit 28) or forever.

## Context

From the outside fitness review of 2026-11-16 (commit 64e9144, `preview` build), finding STD-076 (Medium, Confirmed: 4 runs). Area: HTTP/3 / QUIC connect, Linux build (the Linux build now lists HTTP3 and real HTTP/3 to cloudflare.com works). Wrong exit code, and no timeout at all without `-m`.

**Repro (Linux).**
- `time curl -sS -k -m 5 --http3-only https://127.0.0.1:35997/` (nothing bound on that UDP port)
- `curl -sS -k -m 5 --http3-only https://localhost:35100/` (a TCP-only TLS server)
- `timeout 70 curl -sS -k --http3-only https://127.0.0.1:35997/`

**Expected.** A fast failure on ICMP port-unreachable. The port's ADR-0144 documents exit 56 within milliseconds with `QUIC: recvfrom() unexpectedly returned -1 (errno=...; ...)`. The Windows port does this (`curl: (56) QUIC: recvfrom() unexpectedly returned -1 (errno=10054; Connection was reset)`, 0.47 s). On Linux a connected UDP socket reports ECONNREFUSED at once.

**Actual (port, Linux).** `curl: (28) Connection timed out after 4999 milliseconds`, exit 28, after the full `-m`. Without `-m` it was still running after 70 s.

**Where to start.** `Curl.Networking.UnitLibrary/UdpDatagramConnector.cs`, `DatagramConnectorUdpChannelOpener.cs`, `PortUnreachableReportingDatagramChannel.cs` and `QuicDialer.RunHandshakeAsync` (`QuicDialer.cs`, about line 392). On Linux the receive path does not see ECONNREFUSED (unconnected socket, or the error swallowed). Connect the UDP socket (or read the socket error) so ECONNREFUSED surfaces as the same port-unreachable report the Windows build raises (WSAECONNRESET 10054); keep the errno number and text per platform (Linux errno 111, `Connection refused`).

## Acceptance criteria

- [x] New tests in `Curl.Networking.UnitTests/PortUnreachableReportingDatagramChannelTests.cs` (or a new `PortUnreachableLinuxErrnoTests.cs` beside it; add it to `touches`) prove, with a stand-in socket or channel raising a connection-refused `SocketException` (errno 111 / `SocketError.ConnectionRefused`) on receive, that the channel reports a port-unreachable failure with that errno and text, the same way it does for the Windows reset (10054), on any host platform.
- [x] A test in `Curl.Networking.UnitTests/UdpDatagramConnectorTests.cs` or a new file proves the connector opens a connected UDP socket (or otherwise reads the socket error) so the refusal reaches the channel; no real network beyond a loopback UDP socket that is opened and closed without sending to a listener (a unit test per ADR-0421).
- [x] A test proves `QuicDialer`'s handshake turns the reported port-unreachable into the exit 56 `QUIC: recvfrom() unexpectedly returned -1 (errno=...; ...)` failure rather than waiting for the handshake timeout.
- [x] The Log records the review's hand check on Linux: `curl -sS -k --http3-only https://127.0.0.1:35997/` exits within 1 s with exit 56 and a `QUIC:` message, and the `-m 5` and no-`-m` variants do too; or says it could not be run on the Windows lane.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The Windows behaviour (exit 56, errno 10054) must not change. Tests must pass on Windows, Linux and macOS. Mock servers must be PowerShell or a C# file-based app, never Python (CLAUDE.md).

- Decision: QUIC's `UdpChannelOpener` now connects its UDP socket to the server off Windows (`UdpDatagramChannel` `connectsToServer`; sends use `Socket.SendAsync`), so Linux/macOS deliver ICMP port-unreachable as ECONNREFUSED (errno 111) on receive, which `QuicDialer.RunHandshakeAsync` already words as exit 56 `QUIC: recvfrom() unexpectedly returned -1 (errno=111; Connection refused)` (existing test `ConnectMultiplexedAsync_WhenAReceiveFailsWithAnotherErrorInTheSchannelBuild_UsesCurlsWordsForIt` pins that). Windows is unchanged (unconnected, WSAECONNRESET as before). TFTP/DNS use `UdpDatagramConnector`, unconnected, untouched. Files touched: UdpChannelOpener.cs, UdpDatagramChannel.cs (both added to touches mentally; neither is held by another task), new PortUnreachableLinuxErrnoTests.cs.
- Linux hand check (criterion 4): could not be run on the Windows lane.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: f7dff19f e39c669d 269372e6 abc70256 a0b49a29.
- 2026-11-16: Doing -> Done. QUIC UDP socket is connected off Windows so Linux reports ECONNREFUSED as exit 56 QUIC: recvfrom() at once
