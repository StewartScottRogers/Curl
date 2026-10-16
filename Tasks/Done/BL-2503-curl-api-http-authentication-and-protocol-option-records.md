---
id: BL-2503
title: Curl.Api: HTTP, authentication and protocol option records
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2502]
touches: [Curl.Api/Options/Protocol, Curl.Api.UnitTests/Options/Protocol, Curl.Api/Client/CurlClient.cs, Curl.Api/Messages/CurlClient.SendAsync.cs, Curl.Api/CLAUDE.md]
requirement: none
created: 2026-10-16
completed: 2026-10-16
---
# BL-2503 — Curl.Api: HTTP, authentication and protocol option records

## Goal

Option records for HTTP, authentication and the per-protocol groups (`CurlHttpOptions`, `CurlAuthenticationOptions`, `CurlFtpOptions`, `CurlSshOptions`, `CurlMailOptions` and the rest of `EasyProtocolOptions`) map onto `EasyHttpOptions`, `EasyAuthenticationOptions` and `EasyProtocolOptions`, with the same client-then-request layering.

## Context

Phase 4 of `Documentation/Planning/Curl-Api-And-libcurl.md`; ADR-0506. `Curl.Api` references `Curl.Api.UnitLibrary` (the seam: `ICurlEngine`, `IEasyTransfer`, `EasyOptions`, `TransferInfo`, callbacks) only until the engine-building task; it never references `Libcurl`. Code lives in `Curl.Api/<Area>` with tests in `Curl.Api.UnitTests/<Area>`, namespace `Curl.Api`, BCL only, native-AOT safe (no reflection). Async with `CancellationToken` everywhere; no `.Result` or `.Wait()`. Credentials are never included in an exception message or `ToString()`.

## Acceptance criteria

- [x] Tests assert the `EasyOptions` the fake engine receives for each record member; `ToString()` on an authentication record hides the password; a per-request value overrides the client.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Api` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30); the tests use fakes of the seam interfaces, never the network.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Curl.Api/CLAUDE.md` is kept true.

## Notes

- Touches widened to `Curl.Api/Client/CurlClient.cs` and `Curl.Api/Messages/CurlClient.SendAsync.cs`
  (one line each, calling `CurlProtocolOptions.Apply` beside `CurlTransportOptions.Apply`) and
  `Curl.Api/CLAUDE.md` (the criterion keeps it true). No task in Doing on `origin/work/dark-factory`
  names them (BL-2504 touches `Curl.Api/Cancellation`, BL-2506 `Curl.Api/Mime`).
- Decisions in ADR-0640: records follow ADR-0636; `CurlHttpOptions` leaves headers, method, body,
  MIME and `StreamDependency` to the request; `UserAgent` `null` keeps the client's; flag enums are
  not bit-checked (libcurl takes any bits); `EasyWebSocketOptions` stays `CurlWebSocketOptions`'.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Api` 0 failing members, 100% line and branch,
  worst CRAP 8 (a first run found `ApplyProtocols` at complexity 12 from cached lambdas; replaced by
  the internal `IEasyOptionRecord<TEasy>`).

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: ba7f0b01 4272a6b9 5393a01b 8738c7f6 ae118507.
- 2026-10-16: Doing -> Done. HTTP, authentication and protocol option records map onto the seam with client-then-request layering; Curl.Api 0 failing members; fast tests green.
