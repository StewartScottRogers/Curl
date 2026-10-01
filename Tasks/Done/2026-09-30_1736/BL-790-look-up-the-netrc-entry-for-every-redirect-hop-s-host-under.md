---
id: BL-790
title: Look up the netrc entry for every redirect hop's host under -n
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-505]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-30
---
# BL-790 — Look up the netrc entry for every redirect hop's host under -n

## Goal

Under `-n`/`--netrc-optional`/`--netrc-file` with `-L`, each redirect hop sends the netrc entry for its own host (none when it has none), even under `--location-trusted`, as curl 8.21.0 does, instead of carrying or dropping the first hop's credentials.

## Context

- Found while doing BL-505, which looks the entry up once per URL in `Curl.Console/NetrcCredentialLookup.cs` and puts it on the first hop's context. `Curl.Core.UnitLibrary/RedirectFollower.cs` (`NextHop`) then carries `Credentials` to the same host and drops them for another, unless `--location-trusted`.
- Measured in BL-505 (curl 8.21.0, mingw, Schannel; netrc `machine 127.0.0.1 login nu password np` and `machine localhost login lu password lp`): `-n -L` from 127.0.0.1 to `http://localhost:<P>/b` sends `Basic bHU6bHA=` (lu:lp) on the second hop; with `--location-trusted` the same; with no `localhost` entry the second hop sends no `Authorization` even under `--location-trusted`. Today the tool sends nothing on the first case and `nu:np` on the trusted ones.
- `-u` with a user name is not affected: netrc is not read then, and the existing `--location-trusted` rules stand.

## Acceptance criteria

- [x] `RedirectFollower` takes a per-hop credential lookup (for example a delegate from `CurlUrl` to credentials) that `Curl.Console` supplies from `NetrcCredentialLookup`, so each hop's credentials come from its own host's entry when netrc is in use.
- [x] `Curl.Console.UnitTests` pin the three measured cases above through `HttpProtocolHandler` over a `ScriptedConnector`, including the request bytes of the second hop.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Core.UnitLibrary` and `Curl.Console`.

## Notes

- Plan: a `HopCredentialSelector` delegate (`CurlUrl` -> `NetworkCredential?`) in `Curl.Core.UnitLibrary`, an optional last constructor parameter of `RedirectFollower`. When given, every hop after the first sends the selector's answer, none included, whatever the origin and `--location-trusted`; the bearer token and `-H Authorization:`/`Cookie:` headers keep the existing origin rules. Without one, nothing changes.
- `TransferCredentialLookup.ForRedirectHops` supplies it only when a netrc option is in effect and `-u` gives no user name, running the same `TryLookUp` as the first URL on the hop's URL, so URL user information on the hop merges with the entry as on a first URL.
- Choices taken (defaults, no ADR needed as they follow the measured behaviour): a netrc file problem found on a hop sends that hop no netrc credentials rather than failing it (the first hop already read the file, so this only arises if it changes mid-chain); `-u :pw` with `-n -L` gives hops only what the lookup finds, not `:pw`, as the netrc lookup, not `-u`, governs every hop once netrc is in use.
- Tests: `CurlCommandRunnerNetrcTests` pins the three BL-505 measurements with full second-hop request bytes plus a `-u q:r` case; `RedirectFollowerTests.FollowAsync_CredentialSelector_...` covers the Core seam. Build clean with `-warnaserror`, fast tests green, `Measure-CodeQuality.ps1` 100% line and branch, 0 failing members for `Curl.Core.UnitLibrary` and `Curl.Console`.

## Log

- 2026-09-28: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Under -n with -L each redirect hop sends its own host's netrc entry, or none, --location-trusted or not
