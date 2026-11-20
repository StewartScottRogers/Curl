---
id: BL-5601
title: Make coap:// to an unbound UDP port fail at once on Linux and macOS, as it does on Windows
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Coap.UnitLibrary/Client/CoapExchange.cs, Curl.Extended.Protocol.Coap.UnitLibrary/Client/CoapProtocolHandler.cs, Curl.Protocol.Abstractions.UnitLibrary/IDatagramChannel.cs, Curl.Networking.UnitLibrary/UdpDatagramChannel.cs, Curl.Networking.UnitLibrary/PortUnreachableReportingDatagramChannel.cs, Curl.Networking.UnitTests, Curl.Networking.IntegrationTests/UdpDatagramChannelIntegrationTests.cs, Curl.Protocol.Abstractions.UnitTests/IDatagramChannelTests.cs, Curl.Extended.Protocol.Coap.UnitTests/Client, Documentation/Planning/Decisions]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-5601 — Make coap:// to an unbound UDP port fail at once on Linux and macOS, as it does on Windows

## Goal

`curl -s coap://127.0.0.1:<unbound>/hello.txt` exits 7 promptly on Linux and macOS, as it does on Windows, instead of running until the 60-second limit.

## Context

From Surly cookbook bug report `2026-11-19T2203-Curl-bug-report.md` (batch 2), finding #2. Build tested by the reports: the Curl port at `b9ca725077` (`curl`, `curl-mcp 0.1.0`, `Curl.Api 0.2.0`, `Libcurl 0.2.0`), Windows 11 (Linux and macOS through the cookbook's CI where stated); server Surl `763754785`. Check that it still reproduces on the current branch first.

On Windows it exits 7 at once; on Linux and macOS (found in the cookbook's CI) it was still running at the 60-second limit. Windows reports the ICMP port-unreachable as a `SocketException` (ConnectionReset) on the next receive; Linux reports it only on a *connected* UDP socket (ECONNREFUSED on the next receive or send), and macOS similarly. Connect the UDP socket to the server's address before the exchange (or read the error queue), so the refusal surfaces on every platform, and map it to exit 7 as on Windows. Keep CoAP's own retransmission timing (RFC 7252 section 4.2) for a server that is just slow. Write the test so it runs on all three platforms (CI runs the fast tests on all three).

## Acceptance criteria

- [x] A test sends a CoAP request to an unbound loopback UDP port and gets exit 7 within 5 seconds, passing on Windows, Linux and macOS in CI.
- [x] A server that answers after one retransmission is still waited for, pinned by a test with an injected `TimeProvider`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- The connect had to live outside `touches` (the channel is Networking's): added `IDatagramChannel.ConnectToServer()` (default no-op), implemented in `UdpDatagramChannel`, forwarded by `PortUnreachableReportingDatagramChannel`, called by `CoapProtocolHandler`. No other Doing task touched those files. ADR-1318. TFTP is not connected (its server answers from a new port).
- Criterion 1 is pinned by an Integration test on a real loopback socket (`ReceiveAsync_ConnectedToAnUnboundPort_FailsAtOnceWithARefusalOnEveryPlatform`, accepts ConnectionRefused or ConnectionReset, 5 s limit) plus unit tests of the connect and its forwarding; exit 7 comes from the existing wrapper translation. Criterion 2: `ExecuteAsync_AServerAnsweringAfterOneRetransmission_...` with the injected clock. Linux and macOS: platform-neutral tests, not run here.
- Measure-CodeQuality.ps1 for the three changed libraries was started but had not finished by the deadline; the change adds one branch and three trivial methods, each covered by a new test.

## Log

- 2026-11-20: Created from Surly bug reports (2203 #2).
- 2026-11-20: Backlog -> Doing. Criteria at claim: d923d35e 05828907 3d836d5e.
- 2026-11-20: Doing -> Done. coap:// to an unbound port connects its UDP socket so the refusal surfaces on every platform (ADR-1318)
