---
id: BL-5295
title: Fail the webtransport transfer when the server refuses the CONNECT
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.WebTransport.UnitLibrary/WebTransportProtocolHandler.cs, Curl.Extended.Protocol.WebTransport.UnitLibrary/Session/WebTransportSession.cs, Curl.Extended.Protocol.WebTransport.UnitTests/WebTransportRefusedConnectTests.cs, Documentation/Conformance/Extended/WebTransport.md]
requirement: none
created: 2026-11-16
completed:
---
# BL-5295 — Fail the webtransport transfer when the server refuses the CONNECT

## Goal

When a server answers the port's `webtransport://` extended CONNECT with a non-2xx status (for example `401`), no session exists, and the port's curl exits non-zero and says on stderr that the server refused the session, instead of exiting 0 with empty output.

## Context

Filed from Surl's Phase 7 measurement of the port's extended clients (Stewart approved filing on the port's board, 2026-11-16): Surl `Documentation/Conformance/CurlPort/Port-Board-Handoff.md`, entry 18 (github.com/StewartScottRogers/Surl). Port measured at commit `b42c319ba940f66452b2b6f3e9910d46825b08fe` (port `curl.exe` SHA-256 `F28D1417807AA862BA2A6D5B0BF0CF1200673BE45B959B1A78E1E4B476EF8FA7`), Windows x64, 2026-11-11. Upstream curl has no `webtransport` scheme, so this is judged by the specification, with upstream curl 8.21.0's nearest behaviour as the guide.

- **Finding:** Surl `Documentation/Conformance/CurlPort/Extended.md`, EXT-1 (Surl task BL-462).
- **Surl test:** `PinnedCurlPortClientTalksToSurlOverWebTransportTests.Connect_WithoutCredentials_EchoesNothing` (`Integration`), Surl commit `477dda7d5e31ed2a906b2505853800d227260dc9`.
- **Server:** `surl webtransport://127.0.0.1:<port>/ --self-signed --user alice:secret`; it answers `:status 401` with its challenges, then FIN on the CONNECT stream.
- **Command line:** `curl -k -d "hello refused" webtransport://127.0.0.1:<port>/` (no credentials).
- **Port (defect):** exit 0, empty stdout.
- **What decides:** draft-ietf-webtrans-http3-16 section 3.2: only a 2xx answer to the extended CONNECT establishes a session. Upstream curl 8.21.0's nearest behaviour: a refused WebSocket upgrade fails the transfer rather than exiting 0 (`lib/ws.c`, "Refused WebSockets upgrade"; https://curl.se/libcurl/c/libcurl-errors.html). Which exit code to use is the port's call (BL-5043 chose 22 for a refused SignalR negotiate, matching the WebSocket schemes).
- **Where to start:** `Curl.Extended.Protocol.WebTransport.UnitLibrary/WebTransportProtocolHandler.cs`, `Session/WebTransportSession.cs`. BL-4608's conformance cases include a non-2xx CONNECT; check why they did not catch this.

## Acceptance criteria

- [ ] A port test where the server answers the webtransport CONNECT `401` asserts a non-zero exit code and a stderr message naming the refusal.
- [ ] A port test where the server answers the CONNECT `200` still exits 0 and echoes as before.
- [ ] The chosen exit code is recorded in `Documentation/Conformance/Extended/WebTransport.md`.

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: c0436890 3313b567 3ee6c9dc.
