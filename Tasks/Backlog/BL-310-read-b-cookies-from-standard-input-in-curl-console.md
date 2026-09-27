---
id: BL-310
title: Read -b - cookies from standard input in Curl.Console
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-237]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-310 — Read -b - cookies from standard input in Curl.Console

## Goal

`curl -b - URL` reads Netscape cookies from standard input and sends them, as curl 8.21.0 does.

## Context

- Found in BL-237: `CookieEngine.LoadCookieFilesAsync` opens every `-b` file name through `IFileSystem`, so `-b -` looks for a file named `-` and loads nothing. The man page (https://curl.se/docs/manpage.html#-b) says `-` reads cookies from standard input.
- Standard input is also a `telnet` transfer's upload (`TransferContextFactory`); measure what curl 8.21.0 does when both want it.
- Measure with the mingw curl 8.21.0 and `Record-CurlExchange.ps1`; record commands and bytes in Notes before pinning.

## Acceptance criteria

- [ ] `-b -` with a Netscape cookie file on standard input sends its cookies as measured on curl 8.21.0; a test in `Curl.Console.UnitTests` pins the request bytes.
- [ ] `dotnet build -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for `Curl.Console`.

## Notes

## Log

- 2026-09-26: Created.
