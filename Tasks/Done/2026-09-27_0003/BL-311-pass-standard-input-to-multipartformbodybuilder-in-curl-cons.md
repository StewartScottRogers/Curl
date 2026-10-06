---
id: BL-311
title: Pass standard input to MultipartFormBodyBuilder in Curl.Console for -F @- and <- parts
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-275]
touches: [Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed: 2026-09-27
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

- [x] A `Curl.Console.UnitTests` test runs `-F a=@-` with injected standard input `hello
world` and sees the request body BL-275 pinned (`filename="-"`, no Content-Type, Content-Length 173 with a 46-character boundary).
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports no failing member for `Curl.Console`.

## Notes

- 2026-09-27: The change is one argument: `CurlCommandRunner`'s default `MultipartFormBodyBuilder` now takes the runner's `standardInput`, the stream `Program` opens and already gives telnet and `-T -`. Delivered directly rather than through the full `/feature` stages, because the plan was the task's Context and the change is one line plus tests.
- 2026-09-27: Tests in `CurlCommandRunnerFormTests`: `RunAsync_FormUploadFromStandardInput_SendsStandardInputAsAPartNamedDash` (the BL-275 body, 173 bytes, 46-character random boundary, `filename="-"`, no Content-Type) and `RunAsync_FormContentFromStandardInputOnTwoUrls_SendsTheInputOnceThenAnEmptyPart`. Neither injects a builder, so both go through the production wiring.
- 2026-09-27: Multi-URL and two-part behaviour decided in ADR-0062 (Decided by Claude under Stewart's delegation): the first URL matches curl; a later URL or part gets an empty part with a matching `Content-Length` and exit 0, not curl's mismatched length and exit 26. `Documentation/Planning/Decisions` added to `touches` for the ADR and its index row; no task in Doing names it.
- 2026-09-27: `Measure-CodeQuality.ps1` in its default fast mode flags `DiskWriteOutFileOpener.TryOpen` in `Curl.Console`, which was there before this task: its only tests are `Integration` (real disk). With `-IncludeIntegration`, `Curl.Console` is 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. curl -F a=@- and -F 'a=<-' send piped standard input in the form body
