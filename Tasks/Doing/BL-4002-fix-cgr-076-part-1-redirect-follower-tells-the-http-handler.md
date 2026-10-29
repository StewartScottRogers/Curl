---
id: BL-4002
title: Fix CGR-076 (part 1): redirect follower tells the HTTP handler it withheld a custom Authorization header
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3723]
touches: [Curl.Core.UnitTests/Cgr076RedirectFollowerTests.cs, Curl.Core.UnitLibrary/RedirectFollower.cs, Curl.Protocol.Abstractions.UnitLibrary/HttpRequestOptions.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-4002 — Fix CGR-076 (part 1): redirect follower tells the HTTP handler it withheld a custom Authorization header

## Goal

When a redirect follower withholds a user-set `-H Authorization` value on a redirect to another host, it records that on the request options, so the HTTP handler (BL-4003) can tell it must not generate Basic credentials from the Location's userinfo.

## Context

First half of BL-3605 (CGR-076, `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`): `curl -sSL -H 'Authorization: Bearer xyz' http://127.0.0.1:20500/a` where `/a` answers 302 with `Location: http://x:y@127.0.0.1:20501/ok` sends no Authorization header on the second request in real curl, but Curl sends `Authorization: Basic eDp5`. Read BL-3605 for the full issue text.

## Acceptance criteria

- [ ] `HttpRequestOptions` has an `AuthorizationHeaderWithheld` property, false by default.
- [ ] `RedirectFollower` sets it on the followed request when it strips a user-set Authorization header on a redirect to another host, and leaves it false otherwise.
- [ ] A focused unit test in `Curl.Core.UnitTests/Cgr076RedirectFollowerTests.cs` fails before the change and passes after it (ADR-0482).
- [ ] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` shows Curl.Core and Curl.Protocol.Abstractions inside their gates.

## Notes

- Split from BL-3605 (over its cost cap). Continues the stashed work of BL-3605.
- The stash holds the written but unbuilt code for the follower and the option; apply it, build, test, measure quality.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 0c51b791 64f6828c 2102ff3a 92ceae81.
