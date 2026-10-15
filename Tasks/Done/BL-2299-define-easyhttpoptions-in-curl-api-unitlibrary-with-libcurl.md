---
id: BL-2299
title: Define EasyHttpOptions in Curl.Api.UnitLibrary with libcurl's defaults
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-2292, BL-2301]
touches: [Curl.Api.UnitLibrary/Options/Http, Curl.Api.UnitTests/Options/Http]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2299 — Define EasyHttpOptions in Curl.Api.UnitLibrary with libcurl's defaults

## Goal

`Curl.Api.UnitLibrary/Options/Http` (namespace `Curl.Api.Options.Http`) defines `EasyHttpOptions`, an immutable record carrying every option of the plan's `EasyHttpOptions` row (headers, user agent, referer, encodings, redirects, POST fields and MIME, HTTP version, cookies, alt-svc, HSTS, HTTP/2 stream options), each defaulting to libcurl 8.22.0's default.

## Context

ADR-0506; plan section "CURLOPT groups". Follow the rules for option-group tasks written in BL-2294's Context. `HTTP_VERSION` becomes an enum of `CURL_HTTP_VERSION_*`; `POSTREDIR` a `[Flags]` enum of `CURL_REDIR_POST_*`; `ALTSVC_CTRL` and `HSTS_CTRL` `[Flags]` enums. `POSTFIELDS` is `ReadOnlyMemory<byte>?` (libcurl takes bytes, not text). `MIMEPOST` (and `HTTPPOST`, which formadd builds into the same shape) hold an `EasyMimeBody` defined here: the data model of `curl_mime` - a list of `EasyMimePart`s, each with name, file name, type, encoder, headers, sub-parts and one data source (bytes, a file path, or a data callback typed with the read and seek delegates from `Curl.Api.Callbacks`, BL-2301). The `IMimeBuilder` contract (BL-2314) builds it; the engine maps it onto `Curl.Core`'s `Multipart`. BL-2307 retargets the engine onto this record.

## Acceptance criteria

- [x] `EasyHttpOptions` exists in `Curl.Api.UnitLibrary/Options/Http` and carries every option in the plan's row, each documented with its `CURLOPT_*` name and default.
- [x] Tests in `Curl.Api.UnitTests/Options/Http` pin every default and enum value, and that a `with` copy changes only the property set.
- [x] This task's Notes list each `CURLOPT_*` of the plan's row with the property that carries it (none left without one).
- [x] `dotnet build` is clean (warnings are errors).
- [x] `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged.

## Notes

Shapes decided in ADR-0508. Defaults checked against curl 8.22.0's `docs/libcurl/opts` (HTTP_VERSION `NONE` since 8.13.0, ALTSVC_CTRL 0, FOLLOWLOCATION 0 with `CURLFOLLOW_*` 1-3). Measure-CodeQuality for Curl.Api.UnitLibrary: 0 failing members. Fast tests: 0 failures.

CURLOPT_* to property:
- HTTPHEADER -> `HttpHeaders`; USERAGENT -> `UserAgent`; REFERER -> `Referer`; AUTOREFERER -> `AutoReferer`
- ACCEPT_ENCODING -> `AcceptEncoding`; TRANSFER_ENCODING -> `TransferEncoding`; HTTP_CONTENT_DECODING -> `HttpContentDecoding`; HTTP_TRANSFER_DECODING -> `HttpTransferDecoding`
- FOLLOWLOCATION -> `FollowLocation`; MAXREDIRS -> `MaxRedirects`; POSTREDIR -> `PostRedirect`
- POST -> `Post`; POSTFIELDS and COPYPOSTFIELDS -> `PostFields`; POSTFIELDSIZE(_LARGE) -> `PostFieldSize`; HTTPGET -> `HttpGet`
- MIMEPOST -> `MimePost`; MIME_OPTIONS -> `MimeOptions`; HTTPPOST -> `HttpPost`
- HTTP_VERSION -> `HttpVersion`; HTTP09_ALLOWED -> `Http09Allowed`; IGNORE_CONTENT_LENGTH -> `IgnoreContentLength`; EXPECT_100_TIMEOUT_MS -> `Expect100Timeout`; HTTP200ALIASES -> `Http200Aliases`; REQUEST_TARGET -> `RequestTarget`
- COOKIE -> `Cookie`; COOKIEFILE -> `CookieFiles`; COOKIEJAR -> `CookieJar`; COOKIESESSION -> `CookieSession`; COOKIELIST -> `CookieList`
- ALTSVC -> `AltSvc`; ALTSVC_CTRL -> `AltSvcControl`; HSTS -> `Hsts`; HSTS_CTRL -> `HstsControl`
- PIPEWAIT -> `PipeWait`; STREAM_DEPENDS(_E) -> `StreamDependency`; STREAM_WEIGHT -> `StreamWeight`

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. EasyHttpOptions carries every HTTP CURLOPT of the plan's row with libcurl 8.22.0's defaults, plus the EasyMimeBody curl_mime model
