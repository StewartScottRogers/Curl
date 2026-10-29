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
completed: 2026-10-28
---
# BL-4003 — Fix CGR-076 (part 2): HTTP handler sends no Basic userinfo credentials after a withheld custom Authorization header, and record the real curl comparison

## Goal

Curl behaves as curl 8.22.0 does for every repro in gap report issue CGR-076: after a redirect to another host with userinfo in the Location, no Authorization header (and no Basic) is sent when the user set a custom Authorization header.

## Context

Second half of BL-3605 (CGR-076, `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`); read BL-3605 for the full issue text and repro. BL-4002 adds `HttpRequestOptions.AuthorizationHeaderWithheld`, set by `RedirectFollower`; this task makes the HTTP handler honour it in `HttpRequestHeadFormatter.AppendAuthorization` and `HttpProtocolHandler.ReportAuthorizationLines`.

## Acceptance criteria

- [x] The HTTP handler sends no Authorization header, Basic included, when `AuthorizationHeaderWithheld` is set.
- [x] A focused unit test in `Curl.Protocol.Http.UnitTests/Cgr076Tests.cs` (and `HttpProtocolHandlerTests.Cgr076.cs`) fails before the fix and passes after it (ADR-0482).
- [x] The repro command of CGR-076, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes, and variants that already matched still match.
- [x] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` shows Curl.Protocol.Http inside its gates.

## Notes

- Split from BL-3605 (over its cost cap). Continues the stashed work of BL-3605.
- The stash holds the written but unbuilt handler and formatter code. If BL-4002 already landed the follower and option part of it, apply only what is still missing.
- 2026-10-28 (lane 3): BL-4002 had landed the option and the follower, so only the stash's handler and formatter change and its two test files were applied; they applied cleanly. `Format_AuthorizationHeaderWithheld_SendsNoGeneratedAuthorization(Basic eDp5)` fails before the fix (the Basic line was written, as no `-H` on the hop names Authorization) and passes after it.
- Real curl comparison (curl 8.22.0, Git for Windows ucrt64, against Curl.Console via `-Curl dotnet run --project Curl.Console --no-build --`), both through `Record-CurlExchange.ps1 -Port 18605`, `/a` answering `302` with `Location: http://x:y@localhost:18605/ok` and the second connection `200` with `ok\n`:
  - `curl -sSL -H 'Authorization: Bearer xyz' -H 'Cookie: c=1' -H 'X-Custom: 1' http://127.0.0.1:18605/a`: both exit 0, stdout `ok\n`, stderr empty; the second hop is `GET /ok` to `Host: localhost:18605` with `User-Agent`, `Accept`, `X-Custom: 1` and no `Authorization`. exitcode, stdout, stderr and request bytes are identical.
  - Variant without the `-H Authorization` (`-sSL -H 'X-Custom: 1' ...`), which already matched: both send `Authorization: Basic eDp5` on the second hop; all four fixtures still identical.
- Fast tests green (no failures); `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary`: 0 failing members. The change is platform-neutral (header bytes only, no paths or OS text), so CI on Linux and macOS is expected to match.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 8cccd063 d765a162 4573e9ad 9619a602.
- 2026-10-28: Doing -> Done. A redirect to another host that withholds an -H Authorization value now sends no Basic from the Location's userinfo, as curl 8.22.0 does
