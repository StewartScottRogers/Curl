---
id: BL-2502
title: Curl.Api: transport option records mapped to the easy option groups
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2499]
touches: [Curl.Api/Options/Transport, Curl.Api.UnitTests/Options/Transport, Curl.Api/Client/CurlClient.cs, Curl.Api/Client/CurlClientOptions.cs, Curl.Api/Messages/CurlRequest.cs, Curl.Api/Messages/CurlClient.SendAsync.cs, Curl.Api/CLAUDE.md]
requirement: none
created: 2026-10-16
completed: 2026-10-16
---
# BL-2502 — Curl.Api: transport option records mapped to the easy option groups

## Goal

Option records for connection, DNS, timeouts, TLS and proxy (`CurlNetworkOptions`, `CurlTlsOptions`, `CurlProxyOptions`) map one to one onto `EasyNetworkOptions`, `EasyTlsOptions` and `EasyProxyOptions`, and apply per client and per request, the request winning.

## Context

Phase 4 of `Documentation/Planning/Curl-Api-And-libcurl.md`; ADR-0506. `Curl.Api` references `Curl.Api.UnitLibrary` (the seam: `ICurlEngine`, `IEasyTransfer`, `EasyOptions`, `TransferInfo`, callbacks) only until the engine-building task; it never references `Libcurl`. Code lives in `Curl.Api/<Area>` with tests in `Curl.Api.UnitTests/<Area>`, namespace `Curl.Api`, BCL only, native-AOT safe (no reflection). Async with `CancellationToken` everywhere; no `.Result` or `.Wait()`. Read the three seam option records for the member list; expose the useful members under .NET names and document any seam member left out and why.

## Acceptance criteria

- [x] Tests set each record member and assert the `EasyOptions` the fake engine receives; a per-request value overrides the client; an invalid value (a negative timeout, an unknown TLS version) throws `ArgumentException` or `ArgumentOutOfRangeException` before any transfer.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Api` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30); the tests use fakes of the seam interfaces, never the network.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Curl.Api/CLAUDE.md` is kept true.

## Notes

- Design in ADR-0636 (decided by Claude under Stewart's delegation): `CurlNetworkOptions`,
  `CurlTlsOptions` + `CurlTlsPeerOptions`, `CurlProxyOptions` mirror the seam records member for
  member (seam enums reused); validation runs in each `init` accessor (`TransportOptionGuard`), so an
  invalid record cannot be built and no transfer starts with one. A request's record replaces the
  client's whole group (simplest rule, no `??` per member); `CurlNetworkOptions.ConnectTimeout` is
  `TimeSpan?` so `null` keeps `CurlClientOptions.ConnectTimeout`.
- Seam members left out: `EasyNetworkOptions.Url` and `UrlHandle` (owned by `CurlRequest.Url`) and
  `ConnectOnly` (owned by `ConnectWebSocketAsync`).
- Touches widened: hooking the records in needed `partial` on `CurlClientOptions` and `CurlRequest`
  and one line each in `CurlClient.CreateEasy` and `SendAsync`, plus `Curl.Api/CLAUDE.md` (a
  criterion). No task in Doing on origin/work/dark-factory named those files (BL-2500 and BL-2501
  touch only their own `RequestBodies`/`ResponseBodies` folders).
- Measured: Curl.Api 100% line, 100% branch, 122 members, 0 failing, worst CRAP 8 (the first run
  flagged `CurlTransportOptions.Apply` at complexity 12; split into one helper per group).
- `dotnet format --verify-no-changes` on Curl.Api.UnitTests reports only a pre-existing import-order
  issue in `EngineContracts/Multi/MultiValueTypeTests.cs`, outside this task.

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: 241417af 4272a6b9 5393a01b 8738c7f6 ae118507.
- 2026-10-16: Doing -> Done. Curl.Api's network, TLS and proxy option records map onto the easy option groups per client and per request, validated before any transfer
