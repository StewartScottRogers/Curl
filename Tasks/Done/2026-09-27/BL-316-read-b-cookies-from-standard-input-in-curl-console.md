---
id: BL-316
title: Read -b - cookies from standard input in Curl.Console
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-237]
touches: [Curl.Console, Curl.Console.UnitTests, Record-CurlExchange.ps1]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-316 — Read -b - cookies from standard input in Curl.Console

## Goal

`curl -b - URL` reads Netscape cookies from standard input and sends them, as curl 8.21.0 does.

## Context

- Found in BL-237: `CookieEngine.LoadCookieFilesAsync` opens every `-b` file name through `IFileSystem`, so `-b -` looks for a file named `-` and loads nothing. The man page (https://curl.se/docs/manpage.html#-b) says `-` reads cookies from standard input.
- Standard input is also a `telnet` transfer's upload (`TransferContextFactory`); measure what curl 8.21.0 does when both want it.
- Measure with the mingw curl 8.21.0 and `Record-CurlExchange.ps1`; record commands and bytes in Notes before pinning.

## Acceptance criteria

- [x] `-b -` with a Netscape cookie file on standard input sends its cookies as measured on curl 8.21.0; a test in `Curl.Console.UnitTests` pins the request bytes.
- [x] `dotnet build -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for `Curl.Console`.

## Notes

- `touches` gained `Record-CurlExchange.ps1`: it closed curl's standard input at once, so it
  could not measure `-b -`. No task in Doing named it. It now takes `-StandardInput` (same
  escapes as `-Response`). Windows PowerShell 5.1 has no `ProcessStartInfo.StandardInputEncoding`,
  and the stdin writer put a UTF-8 BOM ahead of the bytes (curl then read the first cookie's
  domain as `﻿127.0.0.1` and dropped it), so the script sets `Console.InputEncoding` to
  Latin-1 while starting curl.
- Measured 2026-09-27, curl 8.21.0 mingw, stdin
  `127.0.0.1\tFALSE\t/\tFALSE\t0\tname\tvalue\n127.0.0.1\tFALSE\t/\tFALSE\t0\tother\ttwo\n`,
  `Record-CurlExchange.ps1 -Port 18316 -StandardInput <that> -CurlArgs ...`:
  - `-b - http://127.0.0.1:18316/x` -> `Cookie: other=two; name=value`, exit 0.
  - `-b - -b s=1 URL` -> `Cookie: other=two; name=value; s=1`.
  - `-b - -b - URL` -> same as one `-b -` (the second reads an empty stdin).
  - empty stdin -> no `Cookie` header.
  - `-b - telnet://127.0.0.1:18316` -> telnet sends all of stdin; so does `-b - -T - file:///...`
    (the file gets all 37 bytes).
  - `-b - telnet://... http://.../x` -> telnet sends stdin, the HTTP request has no `Cookie`.
  - `-b - http://.../x telnet://...` -> HTTP sends both cookies.
  - `-b - -T - http://.../x` -> cookies sent, upload body empty (`0\r\n\r\n` chunked).
- So curl loads `-b` files at the first `http`/`https` transfer, not at the start of the run.
  Implemented as measured, no ADR (no choice left open): `CurlCommandRunner.LoadCookieFilesAsync`
  loads them before the first HTTP transfer (after `-T` resolution, before the upload is read);
  `CookieEngine.LoadCookieFilesAsync` loads once and reads `-` from standard input as Latin-1 to
  its end. For a file (not `-`) the later load is not observable.
- Coverage: `Measure-CodeQuality.ps1 -Library Curl.Console -IncludeIntegration` reports 100/100.
  Without `-IncludeIntegration` it reports 99.48 for `DiskWriteOutFileOpener.TryOpen` only,
  which is pre-existing: its disk tests are `Integration` by BL-280's design. The new code is
  fully covered by the fast run.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. curl -b - reads Netscape cookies from standard input at the first HTTP transfer, as curl 8.21.0 does
