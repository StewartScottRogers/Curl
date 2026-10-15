---
id: BL-2312
title: Define ICurlEngine, IEasyTransfer and the EasyOptions aggregate in Curl.Api.UnitLibrary
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-2293, BL-2294, BL-2295, BL-2296, BL-2297, BL-2298, BL-2299, BL-2300, BL-2301, BL-2302]
touches: [Curl.Api.UnitLibrary/EngineContracts, Curl.Api.UnitTests/EngineContracts, Curl.Api.UnitLibrary/Options/EasyOptions.cs, Curl.Api.UnitTests/Options/EasyOptionsTests.cs]
requirement: none
created: 2026-10-15
completed:
---
# BL-2312 — Define ICurlEngine, IEasyTransfer and the EasyOptions aggregate in Curl.Api.UnitLibrary

## Goal

`Curl.Api.UnitLibrary/EngineContracts` (namespace `Curl.Api.EngineContracts`) defines `ICurlEngine` and `IEasyTransfer`, and `Curl.Api.UnitLibrary/Options/EasyOptions.cs` defines the `EasyOptions` aggregate of every option group - the contract any engine implements for one easy handle.

## Context

ADR-0506, "Layering" and "The engine's input is a CLI-neutral option model"; plan phase 2 step 3. Needs the codes (BL-2293), all option groups (BL-2294 to BL-2300), the callbacks (BL-2301) and `TransferInfo` (BL-2302).

- `EasyOptions`: a `sealed record` with one property per group record (`Behaviour`, `Transfer`, `Network`, `Tls`, `Authentication`, `Proxy`, `Http`, `Protocols`, `Callbacks`), each defaulting to its group's default record. `curl_easy_reset` is "back to `new EasyOptions()`".
- `ICurlEngine` (`IAsyncDisposable`, `curl_global_init`/`cleanup`'s lifetime): `CreateEasy()` returning `IEasyTransfer`; version information (the data `curl_version_info` returns: version, features, protocols, TLS backend) as a record defined here. Multi, share, URL and MIME factories are added by BL-2313 and BL-2314.
- `IEasyTransfer` (`IAsyncDisposable`): `EasyOptions Options { get; set; }`; `ValueTask<CurlCode> PerformAsync(CancellationToken)`; `TransferInfo Info { get; }`; `IEasyTransfer Duplicate()` (`curl_easy_duphandle`: options copied, state not); `void Reset()`; `CurlCode Pause(...)` with a `[Flags]` enum of `CURLPAUSE_*`; `ValueTask<CurlCode> UpkeepAsync(CancellationToken)`; the last error text (`CURLOPT_ERRORBUFFER`'s content). WebSocket, header and connect-only members are added by BL-2315.
- Contracts only: no implementation here except trivial records. Async all the way; no `.Result`.

## Acceptance criteria

- [ ] `ICurlEngine`, `IEasyTransfer`, the pause flags and the version-information record exist in `Curl.Api.UnitLibrary/EngineContracts` with XML docs naming the libcurl function each member stands for.
- [ ] `EasyOptions` exists in `Curl.Api.UnitLibrary/Options/EasyOptions.cs`; a test in `Curl.Api.UnitTests/Options/EasyOptionsTests.cs` shows a new instance equals one built from every group's default record.
- [ ] Tests in `Curl.Api.UnitTests/EngineContracts` pin the `CURLPAUSE_*` values and the version record's defaults.
- [ ] `Curl.Api.UnitLibrary` still references no protocol library and no engine.
- [ ] `dotnet build` is clean (warnings are errors).
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged.

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
