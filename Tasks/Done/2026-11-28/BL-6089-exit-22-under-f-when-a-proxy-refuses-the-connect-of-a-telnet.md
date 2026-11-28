---
id: BL-6089
title: Exit 22 under -f when a proxy refuses the CONNECT of a telnet, dict or gopher tunnel
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-6083]
touches: [Curl.Original.Protocol.Telnet.UnitLibrary/TelnetProtocolHandler.cs, Curl.Original.Protocol.Telnet.UnitTests/TelnetProtocolHandlerProxyConnectFailTests.cs, Curl.Original.Protocol.Dict.UnitLibrary/DictProtocolHandler.cs, Curl.Original.Protocol.Dict.UnitTests/DictProtocolHandlerProxyConnectFailTests.cs, Curl.Original.Protocol.Gopher.UnitLibrary/GopherProtocolHandler.cs, Curl.Original.Protocol.Gopher.UnitTests/GopherProtocolHandlerProxyConnectFailTests.cs, Curl.Original.Protocol.Telnet.UnitLibrary/CLAUDE.md, Curl.Original.Protocol.Dict.UnitLibrary/CLAUDE.md, Curl.Original.Protocol.Gopher.UnitLibrary/CLAUDE.md, Curl.Original.Protocol.Dict.UnitTests/DictProtocolHandlerTransferEventsTests.cs, Curl.Original.Protocol.Gopher.UnitTests/GopherProtocolHandlerTransferEventsTests.cs, Curl.Original.Protocol.Gopher.UnitTests/GopherProtocolHandlerAdversarialTests.cs]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-6089 — Exit 22 under -f when a proxy refuses the CONNECT of a telnet, dict or gopher tunnel

## Goal

`curl -f -p -x <proxy>` for telnet://example.test:23, dict://example.test/d:x and gopher://example.test/1 through a proxy that refuses the CONNECT ends as real curl does: exit 22.

## Context

Measured while doing BL-6083 with `Record-CurlExchange.ps1` (`-sS -v -p -f -x http://127.0.0.1:P <url>`, reply `403`, `Content-Length: 0`): reference curl 8.22.0 writes `* The requested URL returned error: 403`, `* closing connection #0`, `curl: (22) The requested URL returned error: 403`; Curl writes `* CONNECT tunnel failed, response 403` and `curl: (7) CONNECT tunnel failed, response 403`, with no `closing connection` line. BL-6083 did this for FTP: set `ConnectTarget.FailsOnHttpErrorStatus` and map the failed connect (see `FtpProtocolHandler.FailedConnectResult`, the `407` text included). Measure without `-f` too.

## Acceptance criteria

- [x] For each of the three URLs, the `*` lines after the reply head, the error line and the exit code match reference curl, with and without `-f`.
- [x] A focused unit test in each protocol's `.UnitTests` project pins both, `dotnet build` is clean and the fast tests pass.

## Notes

- Measured with `Record-CurlExchange.ps1` (reference curl 8.22.0) before the change: for all three URLs, `-f` with a `403` ends exit 22 `The requested URL returned error: 403`; `-f` with a `407` to a sent Basic credential ends exit 22 `HTTP response code said error`; without `-f` exit 7 `CONNECT tunnel failed, response 403`. Each writes `* closing connection #0` before the error line. After the change Curl's lines from the reply head on were identical (case-sensitive) in all nine cases. `--fail-with-body` keeps exit 7, as BL-6083 pinned for FTP.
- Real curl also writes `* closing connection #0` after a refused TCP connect, an unresolved host (all three schemes) and a failed `gophers` handshake, so the handlers report it for every failed connect, not only a refused CONNECT.
- Direct change by the run, no sub-agents: three small handler edits following BL-6083. No ADR: no design choice beyond matching measured curl.
- `touches` widened (no task in Doing names them): the three libraries' `CLAUDE.md`, to describe the behaviour, and three older tests that asserted no `-v` line on a failed connect.
- `dotnet build` clean; fast tests 58844 passed, 0 failed; `Measure-CodeQuality.ps1` for the three libraries: 0 failing members.
- Platform check: the tests use scripted connectors and no path, OS error or certificate.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing. Criteria at claim: 0a75ad6c 3296538f.
- 2026-11-27: Doing -> Done. telnet, dict and gopher end exit 22 under -f when a proxy refuses the CONNECT, and report closing connection after a failed connect, as curl 8.22.0 does
