---
id: BL-176
title: Send HEAD for -I and fail with exit 22 for -f and --fail-with-body
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-173]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-176 — Send HEAD for -I and fail with exit 22 for -f and --fail-with-body

## Goal

`NoBody` sends HEAD and writes only the header block; `Fail` and `FailWithBody` return exit 22 on a status of 400 or above as curl does.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item H8. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Measured `curl -I`: `HEAD / HTTP/1.1\r\nHost: 127.0.0.1:18082\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n`.
- Measured `curl -f` on 404: exit 22 `curl: (22) The requested URL returned error: 404`, no body written. `--fail-with-body` writes the body then fails 22.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] `NoBody` sends HEAD byte-equal to the measured bytes and writes the header block, no body.
- [x] `Fail` on 404 returns `CurlExitCode.HttpReturnedError` (22) `The requested URL returned error: 404` and writes no body; `FailWithBody` writes it and returns 22.
- [x] Every status >= 400 fails; 401 with credentials is measured on curl 8.21.0 and pinned.
- [x] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`. Tests use the fakes in `Curl.Protocol.Http.UnitTests/Fakes` (added by BL-169), never a socket; every parser test also runs with 1-byte chunks.

## Notes

- Plan item: H8 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Measured 2026-09-26 with curl 8.21.0 (mingw64, Schannel) through `Record-CurlExchange.ps1 -Port 18276`, every command with `-s -S`:
  - `-I http://127.0.0.1:18276/a?b` against `HTTP/1.1 200 OK
Content-Length: 5

hello`: request `HEAD /a?b HTTP/1.1
Host: 127.0.0.1:18276
User-Agent: curl/8.21.0
Accept: */*

`; stdout is the head only; exit 0.
  - `-I -X GET -w %{size_download}`: request line `GET / HTTP/1.1`, stdout the head then `0`: `-I` reads no body whatever `-X` says.
  - `-I -w "%{size_download} %{size_header}"` against a chunked 200 with no chunks: `0 47`, exit 0; no body is read.
  - `-f` on 400, 401, 404, 407, 500: exit 22, stderr `curl: (22) The requested URL returned error: <status>`, empty stdout. `-f` on 399: body written, exit 0.
  - `-f -u a:b` on 401: sends `Authorization: Basic YTpi` (the authenticator's job, BL-181) and still exits 22 with `... error: 401`.
  - `-i -f` on 404: stdout holds the head but no body: the head is written before the failure. `-f -w "%{http_code} %{size_download} %{size_header}"`: `404 0 45`.
  - `--fail-with-body` on 404: stdout `nope!`, exit 22 with the same message; `-w` gives `404 5 45`. With `-i` the head then the body.
  - `-I -f` on 404: head written, exit 22.
  - `-I -d x`: curl refuses on the command line (exit 2) before connecting, so the handler never sees `-I` with a body.
- Decisions (sensible defaults, no ADR needed; all follow the measurements): `HttpRequestFraming.Of` and `HttpRequestHeadFormatter.Format` take `noBody`; a request without a body is HEAD for `-I` unless `-X` names a method, and a body keeps POST (unreachable from the CLI, see above). `-f` fails after the head is written and before any body byte is read, so `%{size_download}` is 0; `--fail-with-body` fails after the body and trailers.
- Renamed `HttpResponseBodyReader`'s `isHeadRequest` parameter to `noBody`: it now means `-I`, whatever method `-X` names, which the old name misstated.
- Gates: `dotnet build` clean; `dotnet test --filter "TestCategory!=Integration"` green (Curl.Protocol.Http.UnitTests 393 passed); `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http*`: 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. -I sends HEAD and writes only the head; -f and --fail-with-body end a 400+ response with exit 22 as curl 8.21.0 does
