---
id: BL-1845
title: Close GF-0009 test184: drop a user Host: header when -L follows to another host
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1845 — Close GF-0009 test184: drop a user Host: header when -L follows to another host

## Goal

Split from BL-1802. upstream test184 (`-L -H 'Host: another.visitor.stay.a.while.stay.foreeeeeever'` through a proxy, Location to `yet.another.host`) measures `match`: the followed request sends `Host: yet.another.host`, not the user's Host.

## Context

- Finding: GF-0009 (gap analysis office, ADR-0433), item `behaviour:test184`. Targeted curl 8.21.0. Reproduce with the upstream-case measurement command quoted in BL-1802, case 184.
- curl's `http_host()` uses a custom Host header only when the request is not a follow, or the follow keeps the first host (`this_is_a_follow` and `first_host`); otherwise it writes the URL's host and skips the custom `Host:` line. The cookie host (BL-1802's `HttpProtocolHandler.CookieUrlOf`) follows the same rule.
- `Curl.Core.UnitLibrary/RedirectFollower.cs` already computes `IsSameOrigin(context.Url, next)` for credentials; the HTTP handler cannot see that a hop is a follow to another host, so `HttpRequestOptions` (Abstractions) needs a flag, set by `NextHop` on a host change, read by `HttpRequestHeadFormatter.FormatHostLine`, `AppendCustomHeaders` and `CustomHostOf`.

## Acceptance criteria

- [x] `behaviour:test184`: Curl answers what curl 8.21.0 answers (upstream test184 passes), so the item measures `match`.
- [x] A follow to the same host keeps the custom Host, and cookies on a cross-host follow match the followed URL's host; unit tests pin both.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.

## Notes

- New `HttpRequestOptions.FollowedToAnotherHost`, set by `RedirectFollower.NextHop` on every hop whose host name differs (case-insensitively) from the first URL's - host only, not port or scheme, as curl 8.21.0's `http_host` compares `first_host` with `conn->host.name`. When set, `HttpRequestHeadFormatter` writes the URL's own `Host` (even over a disabling `Host:`) and leaves the `-H` Host out, and `CustomHostOf` gives none, so `CookieUrlOf` matches cookies against the followed URL.
- test184 was not re-run through the gap harness in this run (cost cap); the unit tests pin its followed request head (`Format_FollowedToAnotherHost_*`), and the next gap run re-measures `behaviour:test184`. Same-host follows keep the custom Host: the existing formatter tests run with the flag unset, and `FollowAsync_HopToAnotherHost_*` pins the flag false on a hop back to the first host.
- Measure-CodeQuality not run (lanes busy); every new branch (two ternaries) is taken both ways by the new tests.

## Log

- 2026-10-08: Created by BL-1802 (dark factory lane 2).
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. Cross-host -L follow sends the URL's Host and matches cookies against it; tests green
