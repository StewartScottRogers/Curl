---
id: BL-1842
title: Close GF-0009 test184: drop a user Host: header when -L follows to another host
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-08
completed:
---
# BL-1842 — Close GF-0009 test184: drop a user Host: header when -L follows to another host

## Goal

Split from BL-1802. upstream test184 (`-L -H 'Host: another.visitor.stay.a.while.stay.foreeeeeever'` through a proxy, Location to `yet.another.host`) measures `match`: the followed request sends `Host: yet.another.host`, not the user's Host.

## Context

- Finding: GF-0009 (gap analysis office, ADR-0433), item `behaviour:test184`. Targeted curl 8.21.0. Reproduce with the upstream-case measurement command quoted in BL-1802, case 184.
- curl's `http_host()` uses a custom Host header only when the request is not a follow, or the follow keeps the first host (`this_is_a_follow` and `first_host`); otherwise it writes the URL's host and skips the custom `Host:` line. The cookie host (BL-1802's `HttpProtocolHandler.CookieUrlOf`) follows the same rule.
- `Curl.Core.UnitLibrary/RedirectFollower.cs` already computes `IsSameOrigin(context.Url, next)` for credentials; the HTTP handler cannot see that a hop is a follow to another host, so `HttpRequestOptions` (Abstractions) needs a flag, set by `NextHop` on a host change, read by `HttpRequestHeadFormatter.FormatHostLine`, `AppendCustomHeaders` and `CustomHostOf`.

## Acceptance criteria

- [ ] `behaviour:test184`: Curl answers what curl 8.21.0 answers (upstream test184 passes), so the item measures `match`.
- [ ] A follow to the same host keeps the custom Host, and cookies on a cross-host follow match the followed URL's host; unit tests pin both.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.

## Notes

## Log

- 2026-10-08: Created by BL-1802 (dark factory lane 2).
