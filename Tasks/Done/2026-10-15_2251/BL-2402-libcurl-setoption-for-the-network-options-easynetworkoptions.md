---
id: BL-2402
title: Libcurl: SetOption for the network options (EasyNetworkOptions)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2399]
touches: [Libcurl/Options/Network, Libcurl.UnitTests/Options/Network]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2402 — Libcurl: SetOption for the network options (EasyNetworkOptions)

## Goal

Every `CURLOPT_*` of `EasyNetworkOptions` (URL, `CURLU`, protocols, port, interface, resolve and DNS options, TCP options, unix sockets, timeouts, connection reuse, `CONNECT_ONLY`, `USE_SSL`) is settable and in the options table.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] A test per option; `CURLOPT_CURLU` takes a `CurlUrlHandle` and stores its engine handle (`CURLU` split to BL-2462, see Notes).
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- Plan: one named static setter per row in `Libcurl/Options/Network/EasyNetworkOptionSetters.cs`, wired by `EasyOptionSetters.Network.cs` (ADR-0565's shape, which keeps the coverage-based complexity low). Range checks, the `PROTOCOLS` bitmask-to-names mapping and the `INT_MAX` cut are ADR-0568. No new public members.
- `CURLOPT_CURLU` was split to BL-2462 before any edit: `CurlUrlHandle` is BL-2438 (Backlog), and `EasyNetworkOptions.UrlFromUrlHandle` is a string, so storing the engine handle needs a `Curl.Api.UnitLibrary` change outside this task's touches. `CURLU` still answers `NotBuiltIn`.
- `TIMEOUT`/`TIMEOUT_MS` are `EasyTransferOptions`, not network, so they are left to the transfer group.
- Measured: `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members; Libcurl.UnitTests 265 passing; full build clean and the fast tests green; `PassingUpstreamCases.txt` untouched.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. Libcurl SetOption sets every EasyNetworkOptions CURLOPT_* but CURLU (BL-2462), with setopt.c's range checks
