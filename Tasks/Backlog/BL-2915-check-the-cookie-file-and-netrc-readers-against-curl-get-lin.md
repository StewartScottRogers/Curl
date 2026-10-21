---
id: BL-2915
title: Check the cookie-file and netrc readers against Curl_get_line's line rules (CurlFileLineReader)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cookies.UnitLibrary, Curl.Cookies.UnitTests, Curl.Authentication.UnitLibrary/NetrcFile.cs, Curl.Authentication.UnitTests/NetrcFileTests.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-2915 — Check the cookie-file and netrc readers against Curl_get_line's line rules (CurlFileLineReader)

## Goal

Curl's cookie-file (`-b <file>`, `-c` re-read) and netrc readers split their files into lines as curl 8.22.0's `Curl_get_line` does, or each difference is fixed with its own unit test.

## Context

BL-2906 added `Curl.Core.UnitLibrary/CurlFileLineReader.cs`, curl 8.22.0's `Curl_get_line` (https://github.com/curl/curl/blob/curl-8_22_0/lib/curl_get_line.c): a last line without a newline gets one, a line that with its newline reaches the `dynbuf` limit fails the read (`CURLE_TOO_LARGE`), and `^Z` is kept. `AltSvcCache.MaxFileLineLength` and `HstsCache`'s line limit already model the alt-svc and HSTS limits (4095). Still unchecked: the cookie reader (curl's `MAX_COOKIE_LINE`, 5000, in `lib/cookie.c`) and `Curl.Authentication.UnitLibrary/NetrcFile.cs` (`lib/netrc.c`, its own `dynbuf` limit). Measure real curl with `Record-CurlExchange.ps1` before pinning, e.g. a cookie file with a 5000-byte line followed by a good line, and a netrc with an over-long line.

## Acceptance criteria

- [ ] The cookie-file reader's handling of an over-long line, a last line without a newline and a `^Z` byte matches curl 8.22.0, pinned by tests in `Curl.Cookies.UnitTests` (each case measured against real curl).
- [ ] `NetrcFile`'s handling of the same three cases matches curl 8.22.0, pinned by tests in `NetrcFileTests.cs`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created (follow-up from BL-2906).
