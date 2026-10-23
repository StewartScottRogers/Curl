---
id: BL-3300
title: Word a receive on a server-closed kept connection 'Connection was aborted' as the Schannel build does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/TlsFailureMessages.cs, Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Networking.UnitTests, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-23
completed:
---
# BL-3300 — Word a receive on a server-closed kept connection 'Connection was aborted' as the Schannel build does

## Goal

When a kept connection the server has closed is reused, Curl on Windows prints the same Recv failure reason as curl 8.22.0 Schannel ("Connection was aborted").

## Context

Found by BL-3282. Record-CurlExchange.ps1 -Connections 3 with a 401 carrying Negotiate, NTLM, Digest and Basic challenges and Content-Length 1, args -sv --anyauth -u u:p: after "Request completely sent off" on the reused connection real curl prints "* Recv failure: Connection was aborted" then "Connection died, retrying a fresh connect"; Curl prints "* Recv failure: Connection was reset". The differential cases proxy-tunnel and proxy-connect-fail (CONNECT answered 200 Content-Length 0 then closed) showed the same in BL-3233's run, though a recorder run of proxy-tunnel gave "reset" from both. Find which WSA error each side sees (Winsock 10053 vs 10054) and why: likely real curl's send hits the RST first so its recv reports WSAECONNABORTED. Keep Linux and macOS on strerror.

## Acceptance criteria

- [ ] Curl's -sv --anyauth exchange above prints "Recv failure: Connection was aborted" on Windows as real curl does, pinned by a focused unit test in the fixed library's .UnitTests project.
- [ ] The fast tests pass and Measure-CodeQuality.ps1 reports 0 failing members for each changed library.

## Notes

## Log

- 2026-10-23: Created.
