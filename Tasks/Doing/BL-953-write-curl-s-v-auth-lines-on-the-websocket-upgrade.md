---
id: BL-953
title: Write curl's -v auth lines on the WebSocket upgrade
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-848]
touches: [Curl.Protocol.Ws.UnitLibrary, Curl.Protocol.Ws.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-953 — Write curl's -v auth lines on the WebSocket upgrade

## Goal

`curl -v` on a `ws://` or `wss://` upgrade that carries or prepares a credential writes the authentication lines curl 8.21.0 writes - `* Server auth using <Scheme> with user '<user>'` before the request, and `* Basic authentication problem, ignoring.` before `Refused WebSocket upgrade: 401` when a Basic value was refused - through the same seam BL-848 gives HTTP.

## Context

- Measured on 2026-09-29 for BL-951 (ADR-0228), curl 8.21.0 mingw/Schannel, `ws://127.0.0.1:18951/c`, `-v -u u:p`:
  - `--digest`: `* Server auth using Digest with user 'u'` after `* using HTTP/1.x`, no `Authorization` sent.
  - `--ntlm`: `* Server auth using NTLM with user 'u'`, then `Authorization: NTLM <Type 1>`.
  - `--basic`: `* Server auth using Basic with user 'u'`, and after the 401 head's last header `* Basic authentication problem, ignoring.` then `* Refused WebSocket upgrade: 401`.
  - `--anyauth`: neither line.
- BL-848 decides how HTTP learns these lines from `Curl.Authentication.UnitLibrary`; reuse that seam in `WsProtocolHandler` rather than inventing another.
- Re-measure with `Record-CurlExchange.ps1` before pinning the exact line order.

## Acceptance criteria

- [ ] A `Curl.Protocol.Ws.UnitTests` event test pins `Server auth using Digest with user 'u'` for `--digest` and `Server auth using NTLM with user 'u'` for `--ntlm`, in the measured position.
- [ ] A `Curl.Protocol.Ws.UnitTests` event test pins `Basic authentication problem, ignoring.` before `Refused WebSocket upgrade: 401` for a refused Basic value.
- [ ] `--anyauth` writes neither line.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ws.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

Filed by BL-951.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
