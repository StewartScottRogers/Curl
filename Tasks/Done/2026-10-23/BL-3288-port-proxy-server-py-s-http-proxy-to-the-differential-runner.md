---
id: BL-3288
title: Port proxy_server.py's HTTP proxy to the differential runner
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3284]
touches: [Curl.Conformance.Differential.UnitLibrary/Protocols/Proxy, Curl.Conformance.Differential.UnitTests/Protocols/Proxy]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3288 — Port proxy_server.py's HTTP proxy to the differential runner

## Goal

An HTTP proxy responder ports `proxy_server.py` (port 18300) byte for byte: forward proxying with its `X-Via: proxy` reply, CONNECT tunnelling to the target, and the `needauth` mode that challenges a request without `Proxy-Authorization`.

## Context

Reference script: `Z:\repos\Curl.difftest\2026-10-22\proxy_server.py` (read only, never run Python). Parent: BL-3234. Reuse BL-3284's shared wire log and CliProtocols host; the responder over a `Stream` in the UnitLibrary, tested with in-memory streams (ADR-0782). The tunnel needs an outbound connection, so inject the connector (a stream pair in unit tests, a `TcpClient` in the host).

## Acceptance criteria

- [x] The proxy responder forwards, tunnels and challenges as `proxy_server.py` does, each covered by unit tests.
- [x] Projects build clean with -warnaserror and their fast tests pass on Windows; every test is platform-neutral (Linux and macOS CI).
- [x] Measure-CodeQuality.ps1 reports 0 failing members for Curl.Conformance.Differential.UnitLibrary.

## Notes

- Split out of BL-3234 by its lane-3 run on 2026-10-23.
- Ported the HTTP half only (forward, CONNECT, needauth); the script's SOCKS4/5 branches are not in this task's goal. The tunnel connector is injected (IProxyTunnelConnector); an unreachable target ends the connection after the 200, as the crashed Python handler did. Host wiring (TcpClient connector, port 18300) is left to the CliProtocols host task.
- Linux/macOS CI: tests use in-memory streams only, platform-neutral.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: d91d38e1 ae951636 5e81fa96.
- 2026-10-23: Doing -> Done. HttpProxyResponder ports proxy_server.py's forward, CONNECT tunnel and needauth modes
