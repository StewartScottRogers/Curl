---
id: BL-315
title: Retry under --retry-max-time, --retry-all-errors, --retry-connrefused and FTP 4xx
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-208]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-315 — Retry under --retry-max-time, --retry-all-errors, --retry-connrefused and FTP 4xx

## Goal

`TransferRetrier` honours `--retry-max-time`, `--retry-all-errors`, `--retry-connrefused` and retries an FTP 4xx failure as curl 8.21.0 does, each with its measured warning text.

## Context

- Found in BL-208 (2026-09-26), which covered `--retry` and `--retry-delay` only. Start at `Curl.Core.UnitLibrary/TransferRetrier.cs` and `RetryPolicy.cs`.
- Upstream `src/tool_operate.c` (`retrycheck`, `retry_sleep`): the reasons print `(retrying all errors)`, `: connection refused` and `: FTP error`; `--retry-max-time` stops retrying once elapsed plus a `Retry-After` would pass it, warning `The Retry-After: time would make this command line exceed the maximum allowed time for retries.`; `--retry-connrefused` needs the OS error to be connection refused, which `TransferResult` does not carry today.
- Measure each against curl 8.21.0 (`/mingw64/bin/curl`) with a loopback server that times requests (BL-208's notes show one) before pinning text or waits.

## Acceptance criteria

- [ ] Each of the four behaviours is measured on curl 8.21.0 and pinned in `TransferRetrierTests` on `FakeTimeProvider`, warning lines included.
- [ ] `dotnet build Curl.Core.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Core`.

## Notes

## Log

- 2026-09-26: Created.
