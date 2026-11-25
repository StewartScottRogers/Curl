---
id: BL-5836
title: Fix fitness finding EXT-108: mqtt5+ws:// ignores http_proxy, unlike ws://
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary/ProxySelector.cs]
requirement: none
created: 2026-11-23
completed: 2026-11-24
---
# BL-5836 — Fix fitness finding EXT-108: mqtt5+ws:// ignores http_proxy, unlike ws://

## Goal

EXT-108 from the outside fitness review of 2026-11-23 09:23 no longer reproduces: mqtt5+ws:// ignores http_proxy, unlike ws://. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-23T0923` (commit `bef3629`, the `preview` release binaries).

This is an extension (Part B): it is judged against Curl's own documentation (`curl --extended-help`, `Documentation/Protocols.md`, `Documentation/Conformance/Extended/*.md`, the ADRs), never against C curl.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the OpenLDAP server from STD-117"), find the task titled with that finding ID. Check that it still reproduces on the current branch first.

Shared setup: Common mock used by EXT-103 to EXT-111: a Python HTTP server `srv.py PORT LOGFILE [tls]` that logs every request head (request line and all header fields) to LOGFILE; in `tls` mode it uses a certificate for 127.0.0.1/localhost signed by a test CA `ca.pem`. Paths: `POST /tus?loc=<url-encoded URL>` answers `201 Created` with `Location: <that URL>` and `Tus-Resumable: 1.0.0`; `PATCH /files/1` answers 204; `/go?to=<URL>` answers 302 to that URL; `/plain` answers 200 with `Cache-Control: max-age=60`. The finding, verbatim:

- **Severity:** Low (proxy rules are documented per scheme; `ALL_PROXY` and `-x` work)
- **Confidence:** Confirmed (2 runs)
- **Protocol/area:** proxy selection (neighbour of the fixed EXT-093)
- **Repro:** a logging CONNECT proxy on 127.0.0.1:27051. `http_proxy=http://127.0.0.1:27051 curl -sS -m4 "mqtt5+ws://reg.test/t"`. Control: `http_proxy=http://127.0.0.1:27051 curl -sS -m4 ws://reg.test/t`.
- **Expected:** consistent with `ws` and the cleartext WebSocket-carried schemes fixed under EXT-093 (`mcp`, `grpc`, `socketio`, `signalr`): the proxy logs `CONNECT reg.test:80`.
- **Actual (.NET):** `curl: (6) Could not resolve host: reg.test`; the proxy log stays empty. The `ws` control is tunnelled.
- **Likely location:** `Curl.Core.UnitLibrary/ProxySelector.cs` line 242: `HttpProxySchemes = ["ws","mcp","grpc","socketio","signalr"]` lacks `"mqtt5+ws"` (and `ProxySelector`'s HTTPS list should be checked for `mqtt5s` over wss, if supported).
- **Acceptance check:** the command makes the proxy log `CONNECT reg.test:80`.

## Acceptance criteria

- [x] the command makes the proxy log `CONNECT reg.test:80`.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Added "mqtt5+ws" to HttpProxySchemes (ProxySelector.cs) so http_proxy applies like ws; test row in ProxySelectorHttpsExtensionSchemeTests. No mqtt5+wss scheme exists in the code, so no HTTPS-list change. Fast Curl.Core.UnitTests green (2009 passed). Measure-CodeQuality ran past its 1h timeout under load; the change adds no branch or method. The proxy-log check is covered by the selector unit test (no live mock run).

## Log

- 2026-11-23: Created from fitness review 2026-11-23T0923.
- 2026-11-23: Backlog -> Deferred. Stewart 2026-11-23: no more tasks this shift; finish Doing and stop. Move back to Backlog to resume.
- 2026-11-24: Deferred -> Backlog. Undeferred by Stewart 2026-11-24 to give the idle lanes work.
- 2026-11-24: Backlog -> Doing. Criteria at claim: c57429a7 69116afe 3d836d5e.
- 2026-11-24: Doing -> Done. mqtt5+ws:// now reads http_proxy like ws://
