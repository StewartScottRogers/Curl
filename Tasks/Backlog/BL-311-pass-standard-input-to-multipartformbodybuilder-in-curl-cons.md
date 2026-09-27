---
id: BL-311
title: Pass standard input to MultipartFormBodyBuilder in Curl.Console for -F @- and <- parts
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-275]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-311 — Pass standard input to MultipartFormBodyBuilder in Curl.Console for -F @- and <- parts

## Goal

`curl.exe -F a=@- URL` and `-F "a=<-" URL` send piped standard input in the form body, because `CurlCommandRunner` gives `MultipartFormBodyBuilder` the standard-input stream `Program` opens.

## Context

- BL-275 (2026-09-26) taught `MultipartFormBodyBuilder` to read `@-`/`<-` parts from an injected `Stream? standardInput` (its fourth constructor parameter). `Curl.Console/CurlCommandRunner.cs` (about line 176) builds it without one, so today those parts still open a file literally named `-` (exit 26 when it does not exist).
- `Program.cs` already opens standard input once and hands it to `TransferContextFactory`; pass the same stream.
- Measured on curl 8.21.0 (`/mingw64/bin/curl`) 2026-09-26: `printf 'hello
world' | curl -F 'a=<-' URL1 URL2` sends `hello
world` to URL1; the URL2 request declares `Content-Length: 159` (the first body's) but carries an empty part (148 bytes) and curl exits 26. Decide and record how far to match that; at minimum the first URL must match.
- Measured the same day: with standard input redirected from a regular file, `curl -F a=@- -F 'b=<-' URL < file` declares `Content-Length: 280` (both parts the file's size), sends the second part empty and exits 26. The builder currently reads standard input whole, as curl does for a pipe, and sends a consistent 269-byte body with exit 0.
- Inject standard input as a `Stream` in tests; never read the real console.

## Acceptance criteria

- [ ] A `Curl.Console.UnitTests` test runs `-F a=@-` with injected standard input `hello
world` and sees the request body BL-275 pinned (`filename="-"`, no Content-Type, Content-Length 173 with a 46-character boundary).
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports no failing member for `Curl.Console`.

## Notes

## Log

- 2026-09-26: Created.
