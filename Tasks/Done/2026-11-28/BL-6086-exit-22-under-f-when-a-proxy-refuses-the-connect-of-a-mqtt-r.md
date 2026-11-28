---
id: BL-6086
title: Exit 22 under -f when a proxy refuses the CONNECT of a mqtt, rtsp or ws tunnel
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-6083]
touches: [Curl.Original.Protocol.Mqtt.UnitLibrary/MqttProtocolHandler.cs, Curl.Original.Protocol.Mqtt.UnitTests/MqttProtocolHandlerProxyConnectFailTests.cs, Curl.Original.Protocol.Rtsp.UnitLibrary/RtspProtocolHandler.cs, Curl.Original.Protocol.Rtsp.UnitTests/RtspProtocolHandlerProxyConnectFailTests.cs, Curl.Original.Protocol.Ws.UnitLibrary/WsProtocolHandler.cs, Curl.Original.Protocol.Ws.UnitTests/WsProtocolHandlerProxyConnectFailTests.cs, Curl.Original.Protocol.Mqtt.UnitLibrary/MqttTransferMessages.cs, Curl.Original.Protocol.Mqtt.UnitLibrary/CLAUDE.md, Curl.Original.Protocol.Rtsp.UnitLibrary/CLAUDE.md, Curl.Original.Protocol.Ws.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-6086 — Exit 22 under -f when a proxy refuses the CONNECT of a mqtt, rtsp or ws tunnel

## Goal

`curl -f -p -x <proxy>` for mqtt://example.test/x, rtsp://example.test/x and ws://example.test/x through a proxy that refuses the CONNECT ends as real curl does: exit 22.

## Context

Measured while doing BL-6083 with `Record-CurlExchange.ps1` (`-sS -v -p -f -x http://127.0.0.1:P <url>`, reply `403`, `Content-Length: 0`): reference curl 8.22.0 writes `* The requested URL returned error: 403`, `* closing connection #0`, `curl: (22) The requested URL returned error: 403`; Curl writes `* CONNECT tunnel failed, response 403` and `curl: (7) CONNECT tunnel failed, response 403`, with no `closing connection` line. BL-6083 did this for FTP: set `ConnectTarget.FailsOnHttpErrorStatus` and map the failed connect (see `FtpProtocolHandler.FailedConnectResult`, the `407` text included). Measure without `-f` too.

## Acceptance criteria

- [x] For each of the three URLs, the `*` lines after the reply head, the error line and the exit code match reference curl, with and without `-f`.
- [x] A focused unit test in each protocol's `.UnitTests` project pins both, `dotnet build` is clean and the fast tests pass.

## Notes

- Measured 2026-11-27 with `Record-CurlExchange.ps1` (`-sS -v -p -x http://127.0.0.1:P`), reference curl against the built Curl: 21 cases (three URLs; `403` with `-f`, with no flag and with `--fail-with-body`; `407` with `-f`; `302` with `-f`; `407` with `-U u:p`, with and without `-f`). Exit code and every stderr line after the reply head are identical in all 21.
- Reference curl also writes `* closing connection #0` after a refused TCP connect and an unresolved host for all three schemes, so each handler writes it after every failed connect, as FTP does (BL-6083); checked on the built Curl for a refused connect on each scheme and an unresolved `ws` host.
- No ADR: the behaviour is curl's, measured, and ADR-1467 already covers the `-f` line on a refused CONNECT.
- `touches` widened to `MqttTransferMessages.cs` (the two exit 22 texts) and the three libraries' `CLAUDE.md`; no other task in Doing named them.
- Gates: `dotnet build` 0 warnings, 0 errors; fast tests green; `Measure-CodeQuality.ps1` for the three libraries, 0 failing members; `dotnet format --verify-no-changes` on the changed files clean.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing. Criteria at claim: 0a75ad6c 3296538f.
- 2026-11-27: Doing -> Done. A refused CONNECT of a mqtt, rtsp or ws tunnel ends exit 22 under -f, and a failed connect writes the closing connection line, as curl does.
