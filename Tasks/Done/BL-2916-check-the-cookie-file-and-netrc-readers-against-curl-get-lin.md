---
id: BL-2916
title: Check the cookie-file and netrc readers against Curl_get_line's line rules (CurlFileLineReader)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cookies.UnitLibrary, Curl.Cookies.UnitTests, Curl.Authentication.UnitLibrary/NetrcFile.cs, Curl.Authentication.UnitLibrary/NetrcTokenScanner.cs, Curl.Authentication.UnitTests/NetrcFileTests.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2916 — Check the cookie-file and netrc readers against Curl_get_line's line rules (CurlFileLineReader)

## Goal

Curl's cookie-file (`-b <file>`, `-c` re-read) and netrc readers split their files into lines as curl 8.22.0's `Curl_get_line` does, or each difference is fixed with its own unit test.

## Context

BL-2906 added `Curl.Core.UnitLibrary/CurlFileLineReader.cs`, curl 8.22.0's `Curl_get_line` (https://github.com/curl/curl/blob/curl-8_22_0/lib/curl_get_line.c): a last line without a newline gets one, a line that with its newline reaches the `dynbuf` limit fails the read (`CURLE_TOO_LARGE`), and `^Z` is kept. `AltSvcCache.MaxFileLineLength` and `HstsCache`'s line limit already model the alt-svc and HSTS limits (4095). Still unchecked: the cookie reader (curl's `MAX_COOKIE_LINE`, 5000, in `lib/cookie.c`) and `Curl.Authentication.UnitLibrary/NetrcFile.cs` (`lib/netrc.c`, its own `dynbuf` limit). Measure real curl with `Record-CurlExchange.ps1` before pinning, e.g. a cookie file with a 5000-byte line followed by a good line, and a netrc with an over-long line.

## Acceptance criteria

- [x] The cookie-file reader's handling of an over-long line, a last line without a newline and a `^Z` byte matches curl 8.22.0, pinned by tests in `Curl.Cookies.UnitTests` (each case measured against real curl).
- [x] `NetrcFile`'s handling of the same three cases matches curl 8.22.0, pinned by tests in `NetrcFileTests.cs`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

- Measured on curl 8.22.0 (mingw, Schannel, Git for Windows ucrt64) on 2026-10-21 with `Record-CurlExchange.ps1`, `curl -s -b <file>` and `curl -sS --netrc-file <file>` against loopback, reading the `Cookie` and `Authorization` headers sent. Cookies: 4998 characters before LF (CR counted) read, 4999 -> no request, exit 100 "A value or data field grew larger than allowed"; same for a last line without LF; `^Z` kept (binary mode), a `^Z`-only line skipped, a cookie holding one refused. Netrc: 16382 bytes + LF read, 16383 -> exit 26 syntax error, anywhere in the file (even after the matching entry ended); bytes counted (UTF-8); on Windows text mode: `^Z` ends the file (`pa^Zss` -> `pa`), CR LF counts as LF (16382 + CR LF read).
- Cookie line rules already matched; the gap was exit 100. The library now reports it (`NetscapeCookieFile.Read(..., out bool isLineTooLong)`, `CookieStore.LoadCookieFile`/`LoadCookieFileAsync` return `false`); failing the transfer is the engine's, filed as BL-2970 (Engine and Console are outside this task's touches). ADR-0738.
- Netrc: `NetrcFile` checks every line up front against `LineBufferLimit` (16384) and reads in text mode on Windows (internal `FindWithReadMode` takes the mode for tests). The new overload was made internal and named apart from `Find` because a second public `Find` made `Unit1304NetrcParsing.cs`'s `<see cref="NetrcFile.Find"/>` ambiguous (CS0419) in a project outside touches.
- Added `Curl.Authentication.UnitLibrary/NetrcTokenScanner.cs` to touches: its lazy per-line check became unreachable (a coverage hole) once `NetrcFile` checks every line first, so it was removed. No task in Doing on `origin/work/dark-factory` named it.
- Off-Windows netrc `^Z` behaviour (kept as a byte) is from `lib/curl_get_line.c`, not measured; pinned by `Find_OffWindows_ReadsInBinaryMode` and `Find_InBinaryMode_ControlZIsKept`.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Cookies.UnitLibrary,Curl.Authentication.UnitLibrary`: both 100% line, 100% branch, max complexity 10, 0 failing members.

## Log

- 2026-10-21: Created (follow-up from BL-2906).
- 2026-10-21: Backlog -> Doing. Criteria at claim: 1413993f a07b7fe6 8e1ce02b.
- 2026-10-21: Doing -> Done. Cookie and netrc readers follow curl 8.22.0's Curl_get_line rules: cookie lines past 4998 reported as CURLE_TOO_LARGE, netrc lines of 16384+ bytes anywhere are a syntax error, netrc read in text mode on Windows
