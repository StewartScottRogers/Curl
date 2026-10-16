---
id: BL-2502
title: Curl.Api: transport option records mapped to the easy option groups
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2499]
touches: [Curl.Api/Options/Transport, Curl.Api.UnitTests/Options/Transport]
requirement: none
created: 2026-10-16
completed:
---
# BL-2502 — Curl.Api: transport option records mapped to the easy option groups

## Goal

Option records for connection, DNS, timeouts, TLS and proxy (`CurlNetworkOptions`, `CurlTlsOptions`, `CurlProxyOptions`) map one to one onto `EasyNetworkOptions`, `EasyTlsOptions` and `EasyProxyOptions`, and apply per client and per request, the request winning.

## Context

Phase 4 of `Documentation/Planning/Curl-Api-And-libcurl.md`; ADR-0506. `Curl.Api` references `Curl.Api.UnitLibrary` (the seam: `ICurlEngine`, `IEasyTransfer`, `EasyOptions`, `TransferInfo`, callbacks) only until the engine-building task; it never references `Libcurl`. Code lives in `Curl.Api/<Area>` with tests in `Curl.Api.UnitTests/<Area>`, namespace `Curl.Api`, BCL only, native-AOT safe (no reflection). Async with `CancellationToken` everywhere; no `.Result` or `.Wait()`. Read the three seam option records for the member list; expose the useful members under .NET names and document any seam member left out and why.

## Acceptance criteria

- [ ] Tests set each record member and assert the `EasyOptions` the fake engine receives; a per-request value overrides the client; an invalid value (a negative timeout, an unknown TLS version) throws `ArgumentException` or `ArgumentOutOfRangeException` before any transfer.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Api` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30); the tests use fakes of the seam interfaces, never the network.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [ ] `Curl.Api/CLAUDE.md` is kept true.

## Notes

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: 241417af 4272a6b9 5393a01b 8738c7f6 ae118507.
