---
id: BL-5487
title: Fix fitness finding EXT-093: mcps://, grpcs:// and socketios:// ignore HTTPS_PROXY; mcp://, grpc://, socketio:// and signalr:// ignore http_proxy
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary/ProxySelector.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5487 — Fix fitness finding EXT-093: mcps://, grpcs:// and socketios:// ignore HTTPS_PROXY; mcp://, grpc://, socketio:// and signalr:// ignore http_proxy

## Goal

EXT-093 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: mcps://, grpcs:// and socketios:// ignore HTTPS_PROXY; mcp://, grpc://, socketio:// and signalr:// ignore http_proxy. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low (consistency; `ALL_PROXY` works)
- **Confidence:** Confirmed
- **Protocol/area:** mcp(s), grpc(s), socketio(s), signalr proxy selection
- **Repro:** a logging CONNECT proxy on 127.0.0.1:47051; `reg.test` does not resolve.
  - `HTTPS_PROXY=http://127.0.0.1:47051 curl -sS -m 5 mcps://reg.test/mcp`; same for `grpcs://reg.test/a.B/C` and `socketios://reg.test/`
  - `http_proxy=http://127.0.0.1:47051 curl -sS -m 5 mcp://reg.test/mcp`; same for `grpc://reg.test/a.B/C`, `socketio://reg.test/`, `signalr://reg.test/hub`
  - Controls: `wss://reg.test/x` with `HTTPS_PROXY` and `ws://reg.test/x` with `http_proxy` use the proxy; `ALL_PROXY` works for all.
- **Expected:** ADR-1297's reason ("always speak HTTPS … as for wss") applies equally: socketios runs over wss, mcps is Streamable HTTP over HTTPS, grpcs is HTTP/2 over TLS. They should honour `https_proxy`/`HTTPS_PROXY`, and their cleartext forms `http_proxy` as `ws` does — or `Documentation/Protocols.md` and each scheme's help must say only `<scheme>_proxy`/`ALL_PROXY` apply.
- **Actual (PORT):** `curl: (6) Could not resolve host: reg.test` (direct); the proxy log is empty.
- **Likely location:** `Curl.Core.UnitLibrary/ProxySelector.cs` (`HttpsProxySchemes` and the `ws` branch in `ProxyVariableNames`).
- **Acceptance check:** each command makes the proxy log `CONNECT reg.test:443` (`:80` for cleartext forms), or an ADR plus help text state the narrower rule.

## Acceptance criteria

- [ ] each command makes the proxy log `CONNECT reg.test:443` (`:80` for cleartext forms), or an ADR plus help text state the narrower rule.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding EXT-093 (Low).
- 2026-11-18: Backlog -> Doing. Criteria at claim: 771cbb2d 69116afe 3d836d5e.
