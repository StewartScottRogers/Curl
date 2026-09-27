---
id: BL-231
title: Register HttpProtocolHandler and fill HttpRequestOptions from the command line
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-173, BL-175, BL-187, BL-188, BL-230]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-231 — Register HttpProtocolHandler and fill HttpRequestOptions from the command line

## Goal

`curl http://...` and `curl https://...` run end to end in `Curl.Console`, with `-X`, `-H`, `-A`, `-e`, the data options, `-G` and `--json` giving curl's request bytes.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item W2. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Measured: default GET `GET /a?b HTTP/1.1\r\nHost: 127.0.0.1:18081\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n`; `-d x=1` `POST / HTTP/1.1\r\nHost: 127.0.0.1:18081\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nContent-Length: 3\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\nx=1`.
- Composition is explicit in `Curl.Console/CurlComposition.cs` (native AOT; no reflection).

## Acceptance criteria

- [x] Over a fake connector (`Curl.Console.UnitTests/ScriptedConnector.cs`), `curl http://...` and `curl https://...` send the measured bytes and write the body to stdout.
- [x] `-X`, `-H`, `-A`, `-e`, `-d`, `--data-*`, `-G` and `--json` each have a test with measured bytes.
- [x] `HttpProtocolHandler` is registered explicitly in `CurlComposition`.
- [x] `dotnet build Curl.Console -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Plan item: W2 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Plan (delivered in-session, 2026-09-26): new `Curl.Console/HttpRequestOptionsMapping.cs` maps `RequestMethod`, `Headers` (+ the `--json` pair), `UserAgent`, `Referer` and `PostData` (a `BytesBody`, `application/x-www-form-urlencoded`, none under `-G`) onto `HttpRequestOptions`; `TransferContextFactory` sets `Http` from it; `CurlCommandRunner` parses `QueryUrl.Append(url, options)` instead of the typed URL; `CurlComposition` registers `HttpProtocolHandler(connector, new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(OperatingSystem.IsWindows())))` with no cookie store. `ScriptedConnector` now records connect targets and written bytes. Tests: `CurlCompositionHttpTests` (25 cases), `TransferContextFactoryTests.Create_HttpRequestOptions_AreMappedOntoHttp`, http/https rows in `CurlCompositionTests`.
- Measured 2026-09-26 with `/mingw64/bin/curl` 8.21.0 (Schannel) as `curl -sS <args>` against a Python loopback recorder on 127.0.0.1:18231 answering `HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello`; https against a Python `ssl` server with a self-signed cert on localhost:18232 with `-k` (`GET /s HTTP/1.1\r\nHost: localhost:18232\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n`). Every command and its request bytes are pinned one row each in `CurlCompositionHttpTests`. Highlights: `--json` adds `Content-Type: application/json` and `Accept: application/json` after every `-H` header (even ones given after it), each skipped when a `-H` header starts with that name and colon, case-insensitive (`-H 'accept: x'`, `-H 'Accept:'` both suppress it), and still sent under `-G`; `-X PUT -d a` sends PUT with the body; `-X PUT -G -d a` sends PUT with `?a` and no body; `-G -d ''` sends no `?`.
- Decision (default taken): `Http` is set on every transfer's context, whatever the scheme; `HttpRequestOptions` documents null and an all-default instance as equivalent, and non-HTTP handlers never read it, so a scheme branch would add a condition for no behaviour. `QueryUrl` is applied to every scheme's URL, as curl's tool applies `-G` / `--url-query` before it knows the protocol.
- Decision (default taken): the authenticator's encoding is `CredentialEncoding.ForPlatform`, the choice ADR-0022 already made; no new ADR was needed because nothing here chose behaviour curl leaves open.
- Gates: `dotnet build Curl.Console -warnaserror` clean; `Measure-CodeQuality.ps1 -Library Curl.Console` (full fast test run with coverage, all green, Curl.Console.UnitTests 298): Curl.Console 100% line, 100% branch, 128 members, 0 failing, worst CRAP 10.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. curl http:// and https:// run end to end through HttpProtocolHandler, with -X, -H, -A, -e, the -d family, -G, --url-query and --json sending curl 8.21.0's measured request bytes
