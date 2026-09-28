---
id: BL-583
title: Register the WebSocket handler for ws and wss in Curl.Console
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-582]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-583 — Register the WebSocket handler for ws and wss in Curl.Console

## Goal

`curl ws://...` and `curl wss://...` run end to end through `Curl.Console` with the WebSocket handler, the connector (and proxies, if BL-579's ADR applies them), and the HTTP options the ADR names, producing the measured request bytes, output and exit codes.

## Context

- Conformance audit 2026-09-28, row 36. Handler: BL-580 to BL-582.
- Register in `Curl.Console/CurlComposition.cs`, map options in `TransferContextFactory.cs`; dispatch in `Curl.Core.UnitLibrary/ProtocolDispatcher.cs` must accept `ws`/`wss` with default ports 80 and 443; add them to the `-V` protocol list as ADR-0021 requires if that list is built here.

## Acceptance criteria

- [x] `Curl.Console.UnitTests` run `ws://127.0.0.1:<P>/` and a `wss://` variant through fake connectors with the bytes BL-579 and BL-582 measured, pinning request bytes, stdout, stderr and exit code, and `-H`/`-u` reaching the upgrade request as the ADR states.
- [x] `curl -V` lists `ws` and `wss`, with a test.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Console` reports 100% line and branch coverage and no failing member.

## Notes

- Built: `CurlComposition.CreateProtocolHandlers` registers `WsProtocolHandler` over the
  end-point-recording connector with `CreateHttpAuthenticator()` (pre-emptive Basic/Bearer for
  `-u`/`--oauth2-bearer`, ADR-0128) and `SystemWebSocketRandomSource`. Dispatch needed no change:
  `ProtocolDispatcher` takes the handler's `SupportedSchemes`, and `CurlUrlScheme` already gives
  `ws` 80 and `wss` 443. `TransferContextFactory` already maps `-H`, `-A`, `-e`, `-X`, `-u`, `-T`,
  `-D` and the proxy into the context; the one change there is that `-i`/`-I` no longer send
  the reply head to stdout for a `ws`/`wss` URL (ADR-0128 row 5).
- `-V`'s protocol list lives in `Curl.Cli.UnitLibrary/CurlVersionText.cs`, so `Curl.Cli.UnitLibrary`
  and `Curl.Cli.UnitTests` were added to `touches` (rule 3; no task in Doing names them). Line now
  ends `telnet tftp ws wss`, as curl 8.21.0's does.
- Measured 2026-09-28, curl 8.21.0 Schannel, `Record-CurlExchange.ps1`, reply the 102-byte `101`
  head + `81 05 "hello"` `88 02 03 e8`:
  - `-u user:pw -H 'X-Test: 1' -w '%{http_code} %{size_download} %{size_header} %{size_request}'
    ws://127.0.0.1:47912/p` → request `GET /p`, `Host`, `Authorization: Basic dXNlcjpwdw==`,
    `User-Agent`, `Accept`, `Upgrade`, `Sec-WebSocket-Version`, `Sec-WebSocket-Key`, `X-Test: 1`,
    `Connection: Upgrade`; stdout `hello` `03 e8` `101 11 102 239`; exit 0. Pinned for `ws` and `wss`.
  - `-I` → `HEAD /` request (193 bytes), no head on stdout, frames not read, `101 0 102 193`,
    exit 52 `Empty reply from server`. The head routing is done here; the `HEAD` method and the
    52 are the handler's, filed as BL-787.
- Tests: `CurlCompositionWsTests` (8), plus `ws`/`wss` rows in `CurlCompositionTests`. The random
  key is checked as base64 of 16 bytes and hidden; the `-T` frame is unmasked with its own mask.
  Console 1281 passed; Cli 2493 passed. `Measure-CodeQuality.ps1 -Library Curl.Console`: 100% line,
  100% branch, 0 failing, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. curl ws:// and wss:// run end to end through Curl.Console with -u/-H in the upgrade request, and -V lists ws wss
