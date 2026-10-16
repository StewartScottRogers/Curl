---
id: BL-2405
title: Libcurl: SetOption for the proxy options (EasyProxyOptions)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2399]
touches: [Libcurl/Options/Proxy, Libcurl.UnitTests/Options/Proxy, Libcurl/CLAUDE.md]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2405 — Libcurl: SetOption for the proxy options (EasyProxyOptions)

## Goal

Every `CURLOPT_*` of `EasyProxyOptions` (proxy, port, type, pre-proxy, `NOPROXY`, tunnel, `PROXYHEADER`, `HEADEROPT`, SOCKS5 options, HAProxy protocol) is settable and in the options table.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] A test per option; a `CurlSlist` value (`PROXYHEADER`) is copied at set time as upstream does.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- Plan: one setter row per `EasyProxyOptions` field in `Libcurl/Options/Proxy/EasyProxyOptionSetters.cs`, wired by the `AddProxySetters` part, following ADR-0572's shape. Decisions in ADR-0576 (setopt.c's checks: PROXYPORT 0..65535 and PROXYTYPE HTTP..SOCKS5_HOSTNAME else BadFunctionArgument, unknown SOCKS5_AUTH bit NotBuiltIn, HEADEROPT keeps the separate bit, HAPROXY_CLIENT_IP turns HAPROXYPROTOCOL on, PROXYHEADER copied at set time).
- `PROXY_SERVICE_NAME` is not set here: `EasyProxyOptions` has no field for it (it is the authentication group's).
- Added `Libcurl/CLAUDE.md` to `touches`: the acceptance criteria ask for it to be kept true, and no task in Doing on origin/work/dark-factory names it.
- Measured: `Measure-CodeQuality.ps1 -Library Libcurl` 0 failing members; Libcurl.UnitTests 390 passed (15 new); full fast run green; PassingUpstreamCases.txt unchanged.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. SetOption sets every EasyProxyOptions CURLOPT_* with setopt.c's checks; PROXYHEADER copied at set time
