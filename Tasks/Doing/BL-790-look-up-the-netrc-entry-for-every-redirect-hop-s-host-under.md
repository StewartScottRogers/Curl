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
completed:
---
# BL-790 — Look up the netrc entry for every redirect hop's host under -n

## Goal

Under `-n`/`--netrc-optional`/`--netrc-file` with `-L`, each redirect hop sends the netrc entry for its own host (none when it has none), even under `--location-trusted`, as curl 8.21.0 does, instead of carrying or dropping the first hop's credentials.

## Context

- Found while doing BL-505, which looks the entry up once per URL in `Curl.Console/NetrcCredentialLookup.cs` and puts it on the first hop's context. `Curl.Core.UnitLibrary/RedirectFollower.cs` (`NextHop`) then carries `Credentials` to the same host and drops them for another, unless `--location-trusted`.
- Measured in BL-505 (curl 8.21.0, mingw, Schannel; netrc `machine 127.0.0.1 login nu password np` and `machine localhost login lu password lp`): `-n -L` from 127.0.0.1 to `http://localhost:<P>/b` sends `Basic bHU6bHA=` (lu:lp) on the second hop; with `--location-trusted` the same; with no `localhost` entry the second hop sends no `Authorization` even under `--location-trusted`. Today the tool sends nothing on the first case and `nu:np` on the trusted ones.
- `-u` with a user name is not affected: netrc is not read then, and the existing `--location-trusted` rules stand.

## Acceptance criteria

- [ ] `RedirectFollower` takes a per-hop credential lookup (for example a delegate from `CurlUrl` to credentials) that `Curl.Console` supplies from `NetrcCredentialLookup`, so each hop's credentials come from its own host's entry when netrc is in use.
- [ ] `Curl.Console.UnitTests` pin the three measured cases above through `HttpProtocolHandler` over a `ScriptedConnector`, including the request bytes of the second hop.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Core.UnitLibrary` and `Curl.Console`.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-30: Backlog -> Doing.
