---
id: BL-2957
title: Hold a -d / --data / --json request body at --limit-rate as curl does (CURLOPT_MAX_SEND_SPEED_LARGE)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2938]
touches: [Curl.Console/CurlCommandRunner.cs, Curl.Console.UnitTests/RateLimitedDataBodyTests.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-2957 — Hold a -d / --data / --json request body at --limit-rate as curl does (CURLOPT_MAX_SEND_SPEED_LARGE)

## Goal

A `-d`, `--data*` or `--json` POST under `--limit-rate N` sends its body no faster than N bytes a second, as curl 8.22.0 does, measured with `Record-CurlExchange.ps1`.

## Context

Found by BL-2938 (AF-0300), which holds a `-T` or `-F` upload at `--limit-rate` (`CurlCommandRunner.SendRateLimited`, `RateLimitedUploadStream`, ADR-0727). A `-d` body reaches the HTTP handler as a `BytesBody` built by `Curl.Engine`'s `HttpRequestOptionsMapping`, not as a stream, so it is still sent at once. curl's `CURLOPT_MAX_SEND_SPEED_LARGE` limits every request body. Measure real curl first (a 30-byte `-d` at `--limit-rate 10`), then decide where to limit it (for example handing the handler a `StreamBody` over the bytes under a limit, checking the `BytesBody`-only paths in `HttpRequestBodyWriter` such as `EarlyResponseWatch` pieces keep their bytes).

## Acceptance criteria

- [ ] A measured `-d` POST at `--limit-rate 10` sends the same request bytes and exit code from Curl as from curl 8.22.0, recorded under Notes.
- [ ] A unit test in `Curl.Console.UnitTests` shows the `-d` body reaching the handler at no more than the rate.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-21: Created.
