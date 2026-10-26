---
id: BL-3851
title: CGR-016 part 3: Linux and macOS percent-encode invalid UTF-8 URL argument bytes as themselves (/%FF, not /%EF%BF%BD)
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3849]
touches: [Curl.Protocol.Abstractions.UnitLibrary/CurlUrl.cs, Curl.Protocol.Abstractions.UnitTests/Cgr016UrlTests.cs, Curl.Engine.UnitLibrary/UrlHandleParts.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3851 — CGR-016 part 3: Linux and macOS percent-encode invalid UTF-8 URL argument bytes as themselves (/%FF, not /%EF%BF%BD)

## Goal

On Linux and macOS, `curl $'http://127.0.0.1:20001/\xff'` requests `GET /%FF`, as curl 8.18.0 does (today `GET /%EF%BF%BD`).

## Context

Split from BL-3545 (third-party gap report `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`, CGR-016). On Linux, .NET hands `Main` an argv already decoded as UTF-8 with every invalid byte replaced by U+FFFD, so every option then re-encodes EF BF BD where curl 8.18.0 (OpenSSL, Linux) uses the raw byte (curl never re-encodes argv). Measure real curl in WSL with `Record-CurlExchange.ps1` before pinning output; no Python.

Needs BL-3849's lossless argument decoding: each invalid byte b arrives as the lone surrogate U+DC00+b. Find where the URL parser percent-encodes non-ASCII path, query and fragment text (`Curl.Protocol.Abstractions.UnitLibrary/CurlUrl.cs`, `Curl.Engine.UnitLibrary/UrlHandleParts.cs`) and encode such a surrogate as `%XX` of its byte rather than as U+FFFD's UTF-8. Measure in WSL whether curl 8.18.0 also keeps the raw byte in a host name or in user and password, and match it.

## Acceptance criteria

- [ ] `curl $'http://127.0.0.1:20001/\xff'` sends the same request line as curl 8.18.0 on Linux; the measured commands and both results are recorded in Notes.
- [ ] URLs with valid UTF-8, and every URL on Windows, encode as before.
- [ ] A focused unit test in `Curl.Protocol.Abstractions.UnitTests/Cgr016UrlTests.cs` fails before the change and passes after it on every platform.
- [ ] Every library changed stays within the coverage, complexity and CRAP gates (`Measure-CodeQuality.ps1` naming them).
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-26: Created.
