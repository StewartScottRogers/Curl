---
id: BL-3265
title: Pin the zone-ID connect-error text for url_effective URLs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3182, BL-3264]
touches: [Curl.Console.UnitTests/CurlCommandRunnerUrlRejectionTests.cs]
requirement: none
created: 2026-10-23
completed:
---
# BL-3265 — Pin the zone-ID connect-error text for url_effective URLs

## Goal

The connect-error text for a zone-ID URL such as `http://[::1%25lo]:9/` is pinned to curl 8.22.0's `Failed to connect to ::1:9`.

## Context

Split from BL-3173 (over its cost cap). Needs BL-3182 (refused-connect fix in `Curl.Networking.UnitLibrary/TcpConnector.cs`) so Curl exits 7 as curl does, and BL-3264 (URL normalisation). Real curl 8.22.0 prints `Failed to connect to ::1:9 after 9 ms: Could not connect to server` with `%{url_effective}` `http://[::1%25lo]:9/`, `%{url.host}` `[::1]`, `%{url.port}` `9`.

## Acceptance criteria

- [ ] A test in `Curl.Console.UnitTests/CurlCommandRunnerUrlRejectionTests.cs` pins the zone-ID connect-error text (`Failed to connect to ::1:9`, exit 7) for `http://[::1%25lo]:9/`, platform-neutral.
- [ ] The fast tests of Curl.Console pass and `dotnet build Curl.Console -warnaserror` is clean.

## Notes

- Split from BL-3173.

## Log

- 2026-10-23: Created.
