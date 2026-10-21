---
id: BL-2907
title: Reject CR and LF in a mail MIME part's name and file name as curl 8.22.0 does, for unit3227
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Api.UnitLibrary/Options/Http/EasyMimePart.cs, Curl.Api.UnitTests/Options/Http/EasyMimePartMailHeaderTests.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-2907 — Reject CR and LF in a mail MIME part's name and file name as curl 8.22.0 does, for unit3227

## Goal

Building a mail (SMTP/IMAP) MIME part whose name or file name holds CR or LF fails with `CURLE_BAD_FUNCTION_ARGUMENT`, as curl 8.22.0's `Curl_mime_prepare_headers` with `MIMESTRATEGY_MAIL` does, while the HTTP form strategy percent-encodes them; the port of `tests/unit/unit3227.c` (BL-2845) can call it.

## Context

Find where Curl builds a MIME part's `Content-Disposition` for mail (`Curl.Api.UnitLibrary/Options/Http/EasyMimePart.cs`, `Curl.Api/Mime/CurlMimePart.cs`, or the engine's multipart builder), add the check, and set `touches` to the file that builds it. Source: https://github.com/curl/curl/blob/curl-8_22_0/lib/mime.c and tests/unit/unit3227.c. A split header line is header injection, so this is a security fix.

## Acceptance criteria

- [ ] Mail parts with `a\r\nX-Injected: 1` as file name, `a\nb` as name or `a\rb` as file name are refused with exit 43; a clean mail part and a CRLF form part give headers with no raw CR or LF.
- [ ] `Curl.Conformance.UnitProgram.UnitLibrary` can call the counterpart (public, or `InternalsVisibleTo Include="Curl.Conformance.UnitProgram.UnitLibrary"` in its library's csproj), so BL-2845 can port the program without touching this library.
- [ ] A focused unit test in the library's own `.UnitTests` project pins every row of the C program's table (ADR-0482).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and the changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created (split from BL-2845, which waits on it).
- 2026-10-21: Backlog -> Doing. Criteria at claim: e4bae39a 3d228e1f d3a9ab15 099d1d27.
