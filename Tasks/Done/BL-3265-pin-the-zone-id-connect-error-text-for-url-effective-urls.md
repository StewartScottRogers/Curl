---
id: BL-3265
title: Pin the zone-ID connect-error text for url_effective URLs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3182, BL-3266]
touches: [Curl.Console.UnitTests/CurlCommandRunnerUrlRejectionTests.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3265 — Pin the zone-ID connect-error text for url_effective URLs

## Goal

The connect-error text for a zone-ID URL such as `http://[::1%25lo]:9/` is pinned to curl 8.22.0's `Failed to connect to ::1:9`.

## Context

Split from BL-3173 (over its cost cap). Needs BL-3182 (refused-connect fix in `Curl.Networking.UnitLibrary/TcpConnector.cs`) so Curl exits 7 as curl does, and BL-3266 (URL normalisation). Real curl 8.22.0 prints `Failed to connect to ::1:9 after 9 ms: Could not connect to server` with `%{url_effective}` `http://[::1%25lo]:9/`, `%{url.host}` `[::1]`, `%{url.port}` `9`.

## Acceptance criteria

- [x] A test in `Curl.Console.UnitTests/CurlCommandRunnerUrlRejectionTests.cs` pins the zone-ID connect-error text (`Failed to connect to ::1:9`, exit 7) for `http://[::1%25lo]:9/`, platform-neutral.
- [x] The fast tests of Curl.Console pass and `dotnet build Curl.Console -warnaserror` is clean.

## Notes

- Split from BL-3173.
- Pipeline run directly: the change is one test, no design to plan. The test drives the runner through a real TcpConnector with a refusing fake dialer and an IPv6-loopback fake resolver, so the text is TcpConnector's own (BL-3182) and nothing touches the OS: platform-neutral, so CI on Linux and macOS is covered by the green fast run here. No production code changed, so no quality run.
- Verified: dotnet build Curl.Console -warnaserror clean; Curl.Console.UnitTests fast run 2855 passed, 25 skipped, 0 failed.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 08c88f15 399a2eb6.
- 2026-10-23: Doing -> Done. The zone-ID connect-error text (exit 7, Failed to connect to ::1:9) is pinned through the runner
