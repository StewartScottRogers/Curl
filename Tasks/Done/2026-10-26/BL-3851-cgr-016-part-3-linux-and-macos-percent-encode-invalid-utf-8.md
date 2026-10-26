---
id: BL-3851
title: CGR-016 part 3: Linux and macOS percent-encode invalid UTF-8 URL argument bytes as themselves (/%FF, not /%EF%BF%BD)
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3849]
touches: [Curl.Protocol.Abstractions.UnitLibrary/CurlUrl.cs, Curl.Protocol.Abstractions.UnitTests/Cgr016UrlTests.cs, Curl.Engine.UnitLibrary/UrlHandleParts.cs, Curl.Protocol.Http.UnitLibrary/HttpUrlText.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3851 — CGR-016 part 3: Linux and macOS percent-encode invalid UTF-8 URL argument bytes as themselves (/%FF, not /%EF%BF%BD)

## Goal

On Linux and macOS, `curl $'http://127.0.0.1:20001/\xff'` requests `GET /%FF`, as curl 8.18.0 does (today `GET /%EF%BF%BD`).

## Context

Split from BL-3545 (third-party gap report `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`, CGR-016). On Linux, .NET hands `Main` an argv already decoded as UTF-8 with every invalid byte replaced by U+FFFD, so every option then re-encodes EF BF BD where curl 8.18.0 (OpenSSL, Linux) uses the raw byte (curl never re-encodes argv). Measure real curl in WSL with `Record-CurlExchange.ps1` before pinning output; no Python.

Needs BL-3849's lossless argument decoding: each invalid byte b arrives as the lone surrogate U+DC00+b. Find where the URL parser percent-encodes non-ASCII path, query and fragment text (`Curl.Protocol.Abstractions.UnitLibrary/CurlUrl.cs`, `Curl.Engine.UnitLibrary/UrlHandleParts.cs`) and encode such a surrogate as `%XX` of its byte rather than as U+FFFD's UTF-8. Measure in WSL whether curl 8.18.0 also keeps the raw byte in a host name or in user and password, and match it.

## Acceptance criteria

- [x] `curl $'http://127.0.0.1:20001/\xff'` sends the same request line as curl 8.18.0 on Linux; the measured commands and both results are recorded in Notes.
- [x] URLs with valid UTF-8, and every URL on Windows, encode as before.
- [x] A focused unit test in `Curl.Protocol.Abstractions.UnitTests/Cgr016UrlTests.cs` fails before the change and passes after it on every platform.
- [x] Every library changed stays within the coverage, complexity and CRAP gates (`Measure-CodeQuality.ps1` naming them).
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Plan: the request target is written by `Curl.Protocol.Http.UnitLibrary/HttpUrlText.cs` (path by runes, query by UTF-8), not by `CurlUrl.cs` or `UrlHandleParts.cs` (the URL API's, already byte-preserving through `UrlByteText`). Added `HttpUrlText.cs` to `touches`: no other task in Doing on origin/work/dark-factory names it or `Curl.Protocol.Http.UnitLibrary`. New `CurlUrl.RequestPath` percent-encodes each `UrlByteText.ToBytes` byte above `~` (so U+DC00+b is `%XX` of b, valid UTF-8 unchanged); `HttpUrlText.Path` is that and `RequestTarget` writes the query's `UrlByteText.ToBytes` bytes. ADR-0823. `UrlHandleParts.cs` unchanged.
- Measured with `Record-CurlExchange.ps1 -Port 18851 -ListenAddress 172.26.96.1 -Curl wsl.exe -CurlArgs '-d','Ubuntu','--','bash','-c',"curl -s $'http://u\xff:p\xff@172.26.96.1:18851/a\xff?q\xff#f\xff'"`, curl 8.18.0 (OpenSSL, Ubuntu): request line `GET /a%FF?q` + raw byte FF + ` HTTP/1.1`, `Authorization: Basic df86cP8=` (u FF : p FF). Before the change Curl sent `/a%EF%BF%BD` and query EF BF BD (what `Rune` enumeration and `Encoding.UTF8` give a lone surrogate); now `RequestPath` gives `/a%FF` and the query bytes are `71 FF` (Cgr016UrlTests). Credentials were already sent as their bytes (ADR-0821, CGR-052).
- Host: `curl $'http://a\xff.example/'` and with `--resolve $'a\xff:18851:172.26.96.1'` both fail with exit 3 `URL using bad/illegal format or missing URL`; `CurlUrl.TryParse` already refuses such a host (pinned in Cgr016UrlTests), so nothing changed there.
- Test note: a lone surrogate in a `[DataRow]` string is lost in the attribute's UTF-8 blob, so the rows write escaped bytes as `^XX`.
- Tests are platform-neutral (strings only, no paths); the fast tests passed on Windows (46 test projects). Windows argv holds no lone surrogates, so Windows URLs encode as before.
- Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary,Curl.Protocol.Http.UnitLibrary: 0 failing members.
## Log

- 2026-10-26: Created.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 0ca0d47c cdf0e834 0054c208 4aabe207 779790c0.
- 2026-10-26: Doing -> Done. Linux and macOS send an invalid UTF-8 URL argument byte as itself (/%FF in the path, raw in the query) as curl 8.18.0 does
