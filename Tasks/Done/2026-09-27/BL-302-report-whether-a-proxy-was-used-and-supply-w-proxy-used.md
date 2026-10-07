---
id: BL-302
title: Report whether a proxy was used and supply -w proxy_used
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-302 — Report whether a proxy was used and supply -w proxy_used

## Goal

`%{proxy_used}` prints `1` when the transfer went through a proxy and `0` otherwise, as curl 8.21.0 does.

## Context

- Measured on 2026-09-26 with curl 8.21.0 (mingw, Schannel); commands and bytes are in BL-284's Notes. ADR-0043 records why BL-284 left these unknown.
- Measured: `0` for file:// and a direct http:// transfer. Measure a transfer through a loopback HTTP proxy (`-x`) and a CONNECT tunnel before pinning `1`.
- The source is the handler, which knows whether it connected to a proxy: record in an ADR a `TransferReport` member (ADR-0015 says a later ADR adds it), set it in the HTTP handler, and print it in `TransferWriteOutVariables`.

## Acceptance criteria

- [x] An ADR names the `TransferReport` member and when it is set.
- [x] `proxy_used` renders `0` for file:// and direct http:// and `1` through a proxy, as measured, pinned in `TransferWriteOutVariablesTests` and the HTTP handler's tests.
- [x] `dotnet build -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for every project touched.

## Notes

- Measured 2026-09-27 with `/mingw64/bin/curl` 8.21.0 (Schannel) against a Python loopback
  origin (18081, answers `200` `ok`) and proxy (18080, forwards with the same reply, and
  answers CONNECT with `200 Connection established` and pipes to 18081):
  - `curl -s -w ' [%{proxy_used}]' http://127.0.0.1:18081/` -> `ok [0]`
  - `curl -s -x http://127.0.0.1:18080 -w ' [%{proxy_used}]' http://example.test/` -> `ok [1]`
  - `curl -s -p -x http://127.0.0.1:18080 -w ' [%{proxy_used} %{http_connect}]' http://example.test/` -> `ok [1 200]`
  - `curl -s -x http://127.0.0.1:18080 --noproxy '*' -w ' [%{proxy_used}]' http://127.0.0.1:18081/` -> `ok [0]`
  - `curl -s -x http://127.0.0.1:1 -w ' [%{proxy_used}] %{exitcode}' http://example.test/` -> ` [1] 7`
  - `curl -s -x socks5://127.0.0.1:1 -w ' [%{proxy_used}] %{exitcode}' http://example.test/` -> ` [1] 7`
  - `file://` with and without `-x` -> `[0]`
- curl reports the proxy the transfer was set to use, even when connecting to it fails. So
  the HTTP handler sets `TransferReport.UsedProxy` whenever `HttpRequestOptions.ForwardProxy`
  is not null, and a failed connect through a proxy returns a report with only that set
  (ADR-0058). `Curl.Console` already leaves `ForwardProxy` null for `--noproxy` and file://.
- Other handlers that tunnel through a proxy (ADR-0056) still report `0`; not in this
  task's `touches` and not measured here.
- Delivered directly rather than through the full `/feature` agent chain: the change is one
  property, one assignment site and one formatter entry.

## Log

- 2026-09-26: Created.
- 2026-09-26: Filed by BL-284.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. -w %{proxy_used} prints 1 for HTTP transfers through a proxy (forwarded, tunnelled or refused) and 0 otherwise, as curl 8.21.0 does
