---
id: BL-2907
title: Reject CR and LF in a mail MIME part's name and file name as curl 8.22.0 does, for unit3227
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary/Multipart/MultipartPartHeaders.cs, Curl.Core.UnitLibrary/Multipart/MultipartFormBodyBuilder.cs, Curl.Core.UnitLibrary/Curl.Core.UnitLibrary.csproj, Curl.Core.UnitTests/Multipart/MultipartPartHeadersLineBreakTests.cs, Curl.Core.UnitTests/Multipart/MultipartFormBodyBuilderNameEscapingTests.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2907 — Reject CR and LF in a mail MIME part's name and file name as curl 8.22.0 does, for unit3227

## Goal

Building a mail (SMTP/IMAP) MIME part whose name or file name holds CR or LF fails with `CURLE_BAD_FUNCTION_ARGUMENT`, as curl 8.22.0's `Curl_mime_prepare_headers` with `MIMESTRATEGY_MAIL` does, while the HTTP form strategy percent-encodes them; the port of `tests/unit/unit3227.c` (BL-2845) can call it.

## Context

Find where Curl builds a MIME part's `Content-Disposition` for mail (`Curl.Api.UnitLibrary/Options/Http/EasyMimePart.cs`, `Curl.Api/Mime/CurlMimePart.cs`, or the engine's multipart builder), add the check, and set `touches` to the file that builds it. Source: https://github.com/curl/curl/blob/curl-8_22_0/lib/mime.c and tests/unit/unit3227.c. A split header line is header injection, so this is a security fix.

## Acceptance criteria

- [x] Mail parts with `a\r\nX-Injected: 1` as file name, `a\nb` as name or `a\rb` as file name are refused with exit 43; a clean mail part and a CRLF form part give headers with no raw CR or LF.
- [x] `Curl.Conformance.UnitProgram.UnitLibrary` can call the counterpart (public, or `InternalsVisibleTo Include="Curl.Conformance.UnitProgram.UnitLibrary"` in its library's csproj), so BL-2845 can port the program without touching this library.
- [x] A focused unit test in the library's own `.UnitTests` project pins every row of the C program's table (ADR-0482).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and the changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

- `touches` retargeted from `Curl.Api.UnitLibrary/Options/Http/EasyMimePart.cs` (a plain data record that builds no header) to `Curl.Core.UnitLibrary/Multipart`, where `MultipartPartHeaders` is Curl's `Curl_mime_prepare_headers` for both `-F` and the libcurl MIME API (via `Curl.Engine`'s `MimePostForm`). No other task in Doing on `origin/work/dark-factory` names those files. `MultipartFormBodyBuilderNameEscapingTests.cs` added too: it pinned 8.21.0's raw CR/LF under `--form-escape`.
- Counterpart for BL-2845's port: `MultipartPartHeaders.RefusesLineBreakInName(part, nameEscaping, forMail)` (the refusal) and `MultipartPartHeaders.Format(...)` (the header block, for `headers_have_ctrl`), both internal; `Curl.Core.UnitLibrary.csproj` now has `InternalsVisibleTo Include="Curl.Conformance.UnitProgram.UnitLibrary"`, which already references Core. `forMail: true` is `MIMESTRATEGY_MAIL`, `forMail: false` with `Percent` is `MIMESTRATEGY_FORM`.
- Behaviour follows 8.22.0's `add_content_disposition`: refused when `MIMESTRATEGY_MAIL` or `mime_formescape` (`--form-escape`, `MultipartNameEscaping.Backslash`) and the name or file name (a file part's base name included) holds CR or LF; a part with its own `Content-Disposition` header gets none, so is not refused. The builder returns exit 43 with `curl_easy_strerror`'s "A libcurl function was given a bad argument" (`LineBreakInNameMessage`). The check runs per part before the part's file is opened; curl prepares all headers at transfer start after the tool has opened files, so a missing file in a later part than a refused one gives 43 here and 26 in curl - an edge left as is (both refuse before sending). Not measured against real curl: the local reference is 8.21.0, which predates the check; the source and unit3227 are the reference.
- Tests: `Curl.Core.UnitTests/Multipart/MultipartPartHeadersLineBreakTests.cs` pins unit3227's five rows plus `--form-escape`, own `Content-Disposition` and a file base name. Fast tests all green; Measure-CodeQuality for Curl.Core.UnitLibrary: 100% line, 100% branch, max complexity 10, 0 failing members.

## Log

- 2026-10-21: Created (split from BL-2845, which waits on it).
- 2026-10-21: Backlog -> Doing. Criteria at claim: e4bae39a 3d228e1f d3a9ab15 099d1d27.
- 2026-10-21: Doing -> Done. Mail MIME parts (and --form-escape parts) with CR or LF in name or file name are refused with exit 43 as curl 8.22.0 does; unit3227 counterpart reachable from Curl.Conformance.UnitProgram.UnitLibrary
