---
id: BL-2503
title: Curl.Api: HTTP, authentication and protocol option records
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2502]
touches: [Curl.Api/Options/Protocol, Curl.Api.UnitTests/Options/Protocol]
requirement: none
created: 2026-10-16
completed:
---
# BL-2503 — Curl.Api: HTTP, authentication and protocol option records

## Goal

Option records for HTTP, authentication and the per-protocol groups (`CurlHttpOptions`, `CurlAuthenticationOptions`, `CurlFtpOptions`, `CurlSshOptions`, `CurlMailOptions` and the rest of `EasyProtocolOptions`) map onto `EasyHttpOptions`, `EasyAuthenticationOptions` and `EasyProtocolOptions`, with the same client-then-request layering.

## Context

Phase 4 of `Documentation/Planning/Curl-Api-And-libcurl.md`; ADR-0506. `Curl.Api` references `Curl.Api.UnitLibrary` (the seam: `ICurlEngine`, `IEasyTransfer`, `EasyOptions`, `TransferInfo`, callbacks) only until the engine-building task; it never references `Libcurl`. Code lives in `Curl.Api/<Area>` with tests in `Curl.Api.UnitTests/<Area>`, namespace `Curl.Api`, BCL only, native-AOT safe (no reflection). Async with `CancellationToken` everywhere; no `.Result` or `.Wait()`. Credentials are never included in an exception message or `ToString()`.

## Acceptance criteria

- [ ] Tests assert the `EasyOptions` the fake engine receives for each record member; `ToString()` on an authentication record hides the password; a per-request value overrides the client.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Api` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30); the tests use fakes of the seam interfaces, never the network.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [ ] `Curl.Api/CLAUDE.md` is kept true.

## Notes

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: ba7f0b01 4272a6b9 5393a01b 8738c7f6 ae118507.
