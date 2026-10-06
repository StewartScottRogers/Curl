---
id: BL-953
title: Write curl's -v auth lines on the WebSocket upgrade
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-848]
touches: [Curl.Protocol.Ws.UnitLibrary, Curl.Protocol.Ws.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-29
completed: 2026-09-30
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

- [x] A `Curl.Protocol.Ws.UnitTests` event test pins `Server auth using Digest with user 'u'` for `--digest` and `Server auth using NTLM with user 'u'` for `--ntlm`, in the measured position.
- [x] A `Curl.Protocol.Ws.UnitTests` event test pins `Basic authentication problem, ignoring.` before `Refused WebSocket upgrade: 401` for a refused Basic value.
- [x] `--anyauth` writes neither line.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ws.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

Filed by BL-951.

- Touches: added `Documentation/Planning/Decisions` for ADR-0278; no task in Doing names it.
- Measured 2026-09-30, curl 8.21.0 Schannel, `Record-CurlExchange.ps1`, `ws://127.0.0.1:18953/c -v`: the Basic problem line goes between the 401's status line and its `WWW-Authenticate: Basic` header (the Context's "after the last header" was that header in the first measurement), once per challenge offering Basic, even with `-H "Authorization: X y"` (which silences `Server auth using Basic`); none on a 403 or a Digest-only 401. `--oauth2-bearer` writes `Server auth using Bearer with user ''` and `Bearer authentication problem, ignoring.` the same way. `--digest` answered by 101 still writes the Digest line. All in ADR-0278.
- Seam: BL-848's `HttpAuthRequest.Events` carries what an authenticator reports while stepping; these lines are decided by the protocol from the request and the value, as HTTP's `HttpAuthUsingLines` decides its own, so `WsAuthUsingLines` and `WsAuthProblemLines` live in the Ws library (it cannot reference the HTTP library). `WsNegotiateInfoLines.ServerAuthUsing` folded into `WsAuthUsingLines`.
- A line is written only when the value's scheme is the one scheme allowed, as curl's picked set before a challenge is the whole wanted set (keeps BL-955's "Basic value with Negotiate|Basic allowed writes no line").
- Verified: the built `curl.exe` writes the same `*`/`<` lines as real curl for `--basic`, `--digest`, `--ntlm`, `--anyauth`. Ws tests 269 passed, 1 skipped (off-Windows); Measure-CodeQuality Ws 100% line, 100% branch, 0 failing members; full fast suite green.
- Follow-up: BL-1040, the same Basic/Bearer problem lines for the HTTP handler.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. curl -v on a ws/wss upgrade writes Server auth using <scheme> and Basic/Bearer authentication problem lines as curl 8.21.0 does
