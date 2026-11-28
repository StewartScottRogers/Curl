---
id: BL-6089
title: Exit 22 under -f when a proxy refuses the CONNECT of a telnet, dict or gopher tunnel
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-6083]
touches: [Curl.Original.Protocol.Telnet.UnitLibrary/TelnetProtocolHandler.cs, Curl.Original.Protocol.Telnet.UnitTests/TelnetProtocolHandlerProxyConnectFailTests.cs, Curl.Original.Protocol.Dict.UnitLibrary/DictProtocolHandler.cs, Curl.Original.Protocol.Dict.UnitTests/DictProtocolHandlerProxyConnectFailTests.cs, Curl.Original.Protocol.Gopher.UnitLibrary/GopherProtocolHandler.cs, Curl.Original.Protocol.Gopher.UnitTests/GopherProtocolHandlerProxyConnectFailTests.cs]
requirement: none
created: 2026-11-27
completed:
---
# BL-6089 — Exit 22 under -f when a proxy refuses the CONNECT of a telnet, dict or gopher tunnel

## Goal

`curl -f -p -x <proxy>` for telnet://example.test:23, dict://example.test/d:x and gopher://example.test/1 through a proxy that refuses the CONNECT ends as real curl does: exit 22.

## Context

Measured while doing BL-6083 with `Record-CurlExchange.ps1` (`-sS -v -p -f -x http://127.0.0.1:P <url>`, reply `403`, `Content-Length: 0`): reference curl 8.22.0 writes `* The requested URL returned error: 403`, `* closing connection #0`, `curl: (22) The requested URL returned error: 403`; Curl writes `* CONNECT tunnel failed, response 403` and `curl: (7) CONNECT tunnel failed, response 403`, with no `closing connection` line. BL-6083 did this for FTP: set `ConnectTarget.FailsOnHttpErrorStatus` and map the failed connect (see `FtpProtocolHandler.FailedConnectResult`, the `407` text included). Measure without `-f` too.

## Acceptance criteria

- [ ] For each of the three URLs, the `*` lines after the reply head, the error line and the exit code match reference curl, with and without `-f`.
- [ ] A focused unit test in each protocol's `.UnitTests` project pins both, `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing. Criteria at claim: 0a75ad6c 3296538f.
