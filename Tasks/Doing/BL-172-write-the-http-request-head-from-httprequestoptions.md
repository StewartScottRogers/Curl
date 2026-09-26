---
id: BL-172
title: Write the HTTP request head from HttpRequestOptions
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-159]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-172 — Write the HTTP request head from HttpRequestOptions

## Goal

An internal request writer produces the request line and headers byte-equal to curl 8.21.0 for the default GET and for `-H`, `-X`, `-A` and `-e`.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item H4. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Measured default GET (`curl http://127.0.0.1:18081/a?b`): `GET /a?b HTTP/1.1\r\nHost: 127.0.0.1:18081\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n`.
- Measured with a custom method and headers (command line not recorded - re-measure with `-X PUT -H 'Accept:' -H 'X-A: 1'` before pinning): `PUT / HTTP/1.1\r\nHost: 127.0.0.1:18082\r\nUser-Agent: curl/8.21.0\r\nX-A: 1\r\n\r\n`.
- curl's `-H` rules: `Name: value` replaces an internal header of that name, `Name:` removes it, `Name;` sends it empty; custom headers follow curl's order (measure). Host drops the default port and brackets IPv6.
- Options come from `ITransferContext.Http` (BL-159).
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] The default GET is byte-equal to the measured bytes above (port substituted); Host omits `:80` / `:443` for the scheme default and brackets an IPv6 literal.
- [ ] `-H` replace, remove (`X:`) and empty (`X;`) each have a test with measured bytes; custom header order matches curl.
- [ ] `CustomMethod` overrides the method; `UserAgent` null sends `curl/8.21.0`, empty omits the header; `Referer` sends `Referer:`; tests for each.
- [ ] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`. Tests use the fakes in `Curl.Protocol.Http.UnitTests/Fakes` (added by BL-169), never a socket; every parser test also runs with 1-byte chunks.

## Notes

- Plan item: H4 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
