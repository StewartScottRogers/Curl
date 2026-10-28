---
id: BL-3950
title: CGR-088 follow-up (SOCKS5): echo curl's failf message as a -v line for SOCKS5 replies other than the GSS-API path
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/TcpConnector.cs, Curl.Networking.UnitLibrary/SocksProxyTunnel.cs, Curl.Networking.UnitLibrary/Socks5Handshake.cs, Curl.Networking.UnitTests/Cgr088Socks5Tests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-3950 — CGR-088 follow-up (SOCKS5): echo curl's failf message as a -v line for SOCKS5 replies other than the GSS-API path

## Goal

Under -v and --trace, SOCKS5 replies other than the GSS-API path (reply codes 1 to 8 and above, bad address type) write curl 8.22.0's failf message as a "* <message>" info line, in curl's order.

## Context

CGR-088 of `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`; split from BL-3880, which the earlier BL-3617 left (read BL-3617's Notes in Tasks/Done for the pattern: echo at the failing site, not in CurlCommandRunner, so lines already written do not repeat). Measure each path with real curl 8.22.0 through `Record-CurlExchange.ps1` before pinning; check the same line appears under `--trace`. Run Measure-CodeQuality.ps1 for the library before committing. BL-3617 added TcpConnector.ReportSocks4Failure, which skips SOCKS5; the GSS-API path already echoes (Socks5GssapiFailureText.cs). Reply codes are CurlProxyCode in Curl.Protocol.Abstractions.UnitLibrary.

## Acceptance criteria

- [ ] Each path, run through real curl 8.22.0 and through Curl with `-v`, gives the same stderr lines; commands and results recorded in Notes.
- [ ] A focused unit test in Curl.Networking.UnitTests/Cgr088Socks5Tests.cs fails before the fix and passes after it (ADR-0482).
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-28: Created.
