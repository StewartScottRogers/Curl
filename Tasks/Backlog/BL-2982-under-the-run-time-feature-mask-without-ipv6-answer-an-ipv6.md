---
id: BL-2982
title: Under the run-time feature mask without IPv6, answer an IPv6 literal (--connect-to [::1]) with CURLE_NOT_BUILT_IN, exit 4 (case 1454)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2981]
touches: [Curl.Networking.UnitLibrary/ConnectDestination.cs, Curl.Networking.UnitTests/ConnectDestinationFeatureMaskTests.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-2982 — Under the run-time feature mask without IPv6, answer an IPv6 literal (--connect-to [::1]) with CURLE_NOT_BUILT_IN, exit 4 (case 1454)

## Goal

With BL-2981's run-time feature mask hiding `IPv6`, `--connect-to localhost:80:[::1]:80 localhost` ends with exit 4 (`CURLE_NOT_BUILT_IN`), as upstream case 1454 expects of a curl built without IPv6.

## Context

Split from BL-2856. The `--connect-to` destination is resolved in `Curl.Networking.UnitLibrary` (`ConnectDestination.cs`; check where the IPv6 literal is read and add the file that does it to `touches` if it is another). curl 8.22.0 `lib/conncache.c` / `lib/url.c` `parse_connect_to_host_port`: without `USE_IPV6` a bracketed IPv6 host answers `CURLE_NOT_BUILT_IN`. Wire the mask from BL-2981 through to that code; without the mask nothing changes.

## Acceptance criteria

- [ ] A unit test in `Curl.Networking.UnitTests` shows that with the mask hiding `IPv6` a `--connect-to` destination naming `[::1]` answers `CURLE_NOT_BUILT_IN`, and without the mask it connects as before.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created.
