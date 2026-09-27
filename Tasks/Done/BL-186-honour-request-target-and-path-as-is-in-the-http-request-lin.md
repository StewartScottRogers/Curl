---
id: BL-186
title: Honour --request-target and --path-as-is in the HTTP request line
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-172, BL-010, BL-293, BL-294]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-186 — Honour --request-target and --path-as-is in the HTTP request line

## Goal

`RequestTarget` replaces the request-line target verbatim, and under `--path-as-is` dot segments reach the request line unsquashed.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item H18. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- `System.Uri` squashes dot segments; BL-010 decides the URL representation that makes `--path-as-is` possible.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] `--request-target '*'` with `-X OPTIONS` and `--path-as-is` with `/a/../b` each match curl 8.21.0's request line (measured).
- [x] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`. Tests use the fakes in `Curl.Protocol.Http.UnitTests/Fakes` (added by BL-169), never a socket; every parser test also runs with 1-byte chunks.

## Notes

- Plan item: H18 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Measured 2026-09-27 with `Record-CurlExchange.ps1` against curl 8.21.0 (mingw, Schannel); every run exited 0 and sent `Host`, `User-Agent: curl/8.21.0`, `Accept: */*` after the request line:
  - `-X OPTIONS --request-target * http://127.0.0.1:18186/a/b?q=1` -> `OPTIONS * HTTP/1.1`
  - `--path-as-is http://127.0.0.1:18187/a/../b` -> `GET /a/../b HTTP/1.1`
  - `http://127.0.0.1:18188/a/../b` -> `GET /b HTTP/1.1`
  - `--request-target /x/../y?z http://127.0.0.1:18189/a` -> `GET /x/../y?z HTTP/1.1`
  - `-x http://127.0.0.1:18190 --request-target * -X OPTIONS http://example.com/a` -> `OPTIONS * HTTP/1.1`, `Host: example.com`, then `Proxy-Connection: Keep-Alive`: the target replaces the absolute form through a forward proxy too.
- Delivered: `HttpRequestHeadFormatter.TargetOf` writes `HttpRequestOptions.RequestTarget` verbatim in place of the origin or absolute form; `HttpProtocolHandler` gives the same target to the authenticator (`HttpAuthRequest.RequestTarget`, Digest's `uri=`), as that record's doc already stated. `--path-as-is` needed no handler change: `CurlUrl` parsed with `pathAsIs` keeps the dot segments and `HttpUrlText.RequestTarget` writes them; the new tests pin it.
- Default taken: a target above ASCII is sent as its UTF-8 bytes, the same rule `HttpUrlText.RequestTarget` applies to a query (ADR-0010). Not measured: the reference curl sees the console code page's bytes, not the text.
- The Console still does not map `--request-target` into `HttpRequestOptions`; BL-245 (Backlog, depends on this task) covers it, and `Curl.Console` is outside this task's `touches`.
- Tests: `HttpProtocolHandlerTests.RequestTarget.cs` (4, with 1-byte and one-read chunks) and 3 in `HttpRequestHeadFormatterTests`. Http tests 761 passed; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary`: 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-26: Created.
- 2026-09-26: Depends on BL-293 and BL-294 as well: ADR-0010 accepted `CurlUrl`, which keeps dot segments under path-as-is (BL-010).
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. The HTTP request line sends --request-target verbatim (origin and proxy form) and keeps --path-as-is dot segments, matching curl 8.21.0
