---
id: BL-4003
title: Fix CGR-076 (part 2): HTTP handler sends no Basic userinfo credentials after a withheld custom Authorization header, and record the real curl comparison
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3723, BL-4002]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests/Cgr076Tests.cs, Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.Cgr076.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-4003 — Fix CGR-076 (part 2): HTTP handler sends no Basic userinfo credentials after a withheld custom Authorization header, and record the real curl comparison

## Goal

Curl behaves as curl 8.22.0 does for every repro in gap report issue CGR-076: after a redirect to another host with userinfo in the Location, no Authorization header (and no Basic) is sent when the user set a custom Authorization header.

## Context

Second half of BL-3605 (CGR-076, `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`); read BL-3605 for the full issue text and repro. BL-4002 adds `HttpRequestOptions.AuthorizationHeaderWithheld`, set by `RedirectFollower`; this task makes the HTTP handler honour it in `HttpRequestHeadFormatter.AppendAuthorization` and `HttpProtocolHandler.ReportAuthorizationLines`.

## Acceptance criteria

- [ ] The HTTP handler sends no Authorization header, Basic included, when `AuthorizationHeaderWithheld` is set.
- [ ] A focused unit test in `Curl.Protocol.Http.UnitTests/Cgr076Tests.cs` (and `HttpProtocolHandlerTests.Cgr076.cs`) fails before the fix and passes after it (ADR-0482).
- [ ] The repro command of CGR-076, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes, and variants that already matched still match.
- [ ] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` shows Curl.Protocol.Http inside its gates.

## Notes

- Split from BL-3605 (over its cost cap). Continues the stashed work of BL-3605.
- The stash holds the written but unbuilt handler and formatter code. If BL-4002 already landed the follower and option part of it, apply only what is still missing.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 8cccd063 d765a162 4573e9ad 9619a602.
