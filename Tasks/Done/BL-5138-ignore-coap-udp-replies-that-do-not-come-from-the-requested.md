---
id: BL-5138
title: Ignore coap:// UDP replies that do not come from the requested server's address and port (EXT-046)
priority: High
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Extended.Protocol.Coap.UnitLibrary/Client/CoapExchange.cs, Curl.Extended.Protocol.Coap.UnitLibrary/Observe/CoapObservation.cs, Curl.Extended.Protocol.Coap.UnitTests/Client, Documentation/Conformance/Extended/Coap.md]
requirement: none
created: 2026-11-13
completed: 2026-11-13
---
# BL-5138 — Ignore coap:// UDP replies that do not come from the requested server's address and port (EXT-046)

## Goal

A `coap://` exchange takes a datagram as the response only when its source address and port are the server endpoint the request went to, so an off-path datagram cannot supply the answer.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding EXT-046 (Medium, Confirmed, twice per scheme). Weakens security: off-path response spoofing. The DNS half of the same finding is a separate task (`Curl.Extended.Protocol.DnsQuery`); do not touch it here, and do not change `Curl.Networking.UnitLibrary/UdpDatagramChannel.cs` (unconnected on purpose for TFTP's RFC 1350 transfer IDs).

**Repro.** A UDP server on 127.0.0.1:27062 (write it in PowerShell or a C# file-based app; never Python): for each CON request, a second socket bound to 127.0.0.2:27063 first sends an ACK 2.05 with the request's Message ID and token and payload `SPOOFED`; 0.3 s later 127.0.0.1:27062 sends the same ACK with payload `genuine`. Run `curl -sS -m 4 coap://127.0.0.1:27062/x`.

**Expected.** RFC 7252 section 5.3.2: "The source endpoint of the response MUST be the same as the destination endpoint of the original request." Output `genuine`, exit 0. `Documentation/Conformance/Extended/Coap.md` row `Coap:RFC7252-5.3.2-3` is marked n/a on the claim that curl's datagram channel is connected to the server's endpoint, which is not true: the channel is unconnected.

**Actual.** `SPOOFED`, exit 0. A reply from another port on the same IP is also accepted.

**Where to start.**
- `Curl.Extended.Protocol.Coap.UnitLibrary/Client/CoapExchange.cs`, `AwaitResponseAsync` (about lines 64-102): `channel.ReceiveAsync` returns `DatagramReceived(Length, RemoteEndPoint)`, and only the length is used (`Decoded(buffer.AsSpan(0, ...Length))`, then `Classify`). Compare `RemoteEndPoint` with `channel.ServerEndPoint` (address and port; an IPv4-mapped IPv6 address equals its IPv4 form) and drop a mismatching datagram, keeping the same pending receive loop and timers. The same check applies wherever the Observe notification loop (`Observe/CoapObservation.cs`) receives datagrams through this exchange; if it receives separately, apply the check there too and add that file to this task's `touches`.
- Do not break the DTLS path (`coaps`): check how its channel reports `RemoteEndPoint` before comparing.
- Correct the `Coap:RFC7252-5.3.2-3` row so it names a covering test instead of n/a.

## Acceptance criteria

- [x] New tests in `Curl.Extended.Protocol.Coap.UnitTests/Client/CoapResponseSourceEndpointTests.cs` (new, using the existing `ScriptedDatagramChannel`) prove that a valid ACK 2.05 (right Message ID and token) from another IP is ignored and the genuine reply after it is the response.
- [x] A test proves the same for a reply from the right IP but another port, and a test proves that when only spoofed replies arrive the exchange retransmits and times out as if nothing came.
- [x] A test proves a reply from the server endpoint written in IPv4-mapped IPv6 form is still accepted; the existing CoAP tests (including Observe and DTLS) stay green.
- [x] `Documentation/Conformance/Extended/Coap.md` row `Coap:RFC7252-5.3.2-3` no longer says n/a and its "Covered by" names a test from this task.
- [x] The tests are platform-neutral (no real socket, no 127.0.0.2).
- [x] `dotnet build Curl.Extended.Protocol.Coap.UnitLibrary -warnaserror` is clean, `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The review's mock servers were Python; use PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python. Keep the 100% line and branch coverage gate.
- Review acceptance check, for a hand check: `curl -sS -m 4 coap://127.0.0.1:27062/x` prints `genuine` and exits 0.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: 6a5b5e07 f94b0943 6833b787 18610595 aa6fc788 d3bdf191.

## Notes (run)

- Observe loop (`Observe/CoapObservation.cs`) receives separately, so the same check (`CoapExchange.IsFromServer`) applies there; added to `touches`. No other Doing task names it.
- DTLS: `DtlsDatagramChannel` reports `transport.ServerEndPoint` as the source, so coaps is unaffected.
- Platform-neutral: tests use scripted channels only. CI on Linux/macOS criterion met by that. Measure-CodeQuality on the Coap library: 0 failing members.
- 2026-11-13: Doing -> Done. coap responses from another endpoint are ignored; tests added
