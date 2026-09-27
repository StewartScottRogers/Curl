---
id: BL-273
title: Read Set-Cookie header lines in cookie files
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-221]
touches: [Curl.Cookies.UnitLibrary, Curl.Cookies.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-273 — Read Set-Cookie header lines in cookie files

## Goal

`NetscapeCookieFile` reads a `Set-Cookie:` header line in a `-b` file the way curl 8.21.0 does, instead of refusing it as a malformed line.

## Context

- Found in BL-221 (2026-09-26). curl's `cookie_load` treats a line starting `Set-Cookie:` as an HTTP header and hands it to the header parser with no request host; BL-221's `NetscapeCookieFile.ParseLine` refuses it today (it has too few tab fields).
- Start at `Curl.Cookies.UnitLibrary/NetscapeCookieFile.cs` and `SetCookieParser.cs`. Upstream: https://curl.se/docs/http-cookies.html, curl 8.21.0.
- Measure first: run curl 8.21.0 (`/mingw64/bin/curl`) with `-b` on a file mixing Netscape lines and `Set-Cookie:` lines (with and without `Domain`, `Path`, `Expires`, in varying case of the prefix), against `Record-CurlExchange.ps1`, and record the `Cookie` header sent and the `-c` jar written.

## Acceptance criteria

- [ ] A `NetscapeCookieFileTests` test loads a file with `Set-Cookie:` lines and matches the measured `-c` jar byte for byte.
- [ ] A test pins the measured `Cookie` header curl sends for such a file.
- [ ] `dotnet build Curl.Cookies.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cookies`.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
