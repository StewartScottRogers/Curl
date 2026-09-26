---
id: BL-271
title: Fail a -L hop whose redirect URL does not parse with exit 1 instead of throwing
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-179]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-271 — Fail a -L hop whose redirect URL does not parse with exit 1 instead of throwing

## Goal

Under `-L`, a 3xx whose `TransferReport.RedirectUrl` is not a valid URL (for example `Location: http://[bad`) ends the transfer with exit 1 and curl's message instead of throwing `UriFormatException` from `RedirectFollower`.

## Context

- Found in BL-179 (2026-09-26). The HTTP handler reports a `Location` that does not parse exactly as curl 8.21.0's `%{redirect_url}` does: whole and unchanged (measured: `Location: http://[bad` gives `%{redirect_url}` `http://[bad`).
- `Curl.Core.UnitLibrary/RedirectFollower.cs` does `Uri next = new(target);` before its refusal checks, so that value throws.
- Measure curl 8.21.0 with `-L` against a server sending `Location: http://[bad` for the exact exit code and message before pinning them; the follower already uses `The redirect target URL could not be parsed: ...` for an unknown scheme.

## Acceptance criteria

- [ ] A `RedirectFollowerTests` test with a hop reporting `RedirectUrl = "http://[bad"` and `ResponseCode = 302` gets the measured exit code and message, and no exception.
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-09-26: Created.
