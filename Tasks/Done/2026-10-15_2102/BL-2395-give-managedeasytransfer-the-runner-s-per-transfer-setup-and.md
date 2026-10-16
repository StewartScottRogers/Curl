---
id: BL-2395
title: Give ManagedEasyTransfer the runner's per-transfer setup and keep the last perform's whole TransferResult
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2318]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2395 — Give ManagedEasyTransfer the runner's per-transfer setup and keep the last perform's whole TransferResult

## Goal

`ManagedEasyTransfer` keeps the whole `TransferResult` of its last perform from options for the curl tool to read, and can take the runner's already-chosen pieces (proxy and looked-up credentials, SSH options, `If-None-Match` lines, `-r` range and `-C` offset) instead of choosing them itself.

## Context

Split out of BL-2394 (ADR-0506 phase 2, ADR-0552) by its lane on 2026-10-15: the runner's serial attempt context (`CurlCommandRunner`, the two `transferContextFactory.Create` calls near lines 3533 and 4377, and `FollowRetryingThroughAsync`) carries about twenty tool-side pieces, too many for one run. Each step adds the capability to `ManagedEasyTransfer.PerformFromOptionsAsync` (internal properties the tool sets, as `ToolSettings`, `Host` and `TraceComponents` already are) and tests it in `Curl.Engine.UnitTests`; `Curl.Console` keeps using `IToolAttemptTransfer.ToolAttempt` until BL-2394 switches the runner over and deletes it. `Curl.Api` and `Libcurl` handles, which set none of the new properties, must behave exactly as today.

This step: decide in an ADR ("Decided by Claude under Stewart's delegation") how the runner gets the whole `TransferResult` for `--retry`, its failure lines and `-w` once `ToolAttempt` is gone (`Info` alone does not carry the report) - e.g. an internal `LastResult` on the handle - and implement it for `PerformFromOptionsAsync`. Add the internal inputs the runner computes per transfer before connecting (`TrySelectProxyAndCredentials`'s proxy and credentials, `ResolveSshOptionsAsync`'s SSH options, `--etag-compare`'s `If-None-Match` lines, the parsed range, the resolved `-C` offset, the `-F` body, the `-T` upload stream) so the handle's `CreateContext` passes them to `TransferContextFactory.Create` as the runner does today. Also let the tool turn off the handle's own cookie, alt-svc and HSTS setup when the runner already did it, or document why the handle's setup replaces the runner's.

## Acceptance criteria

- [x] An ADR records how the curl tool reads the last perform's whole `TransferResult`, and `ManagedEasyTransferTests` pins it for a perform from options.
- [x] A perform from options with each new internal input set puts it on the `TransferContext` exactly as the runner's `transferContextFactory.Create` call does, each pinned by a test.
- [x] A handle with none of them set behaves as before (the existing `ManagedEasyTransferTests` pass unchanged).
- [x] `dotnet build` is clean and the fast tests pass; `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members.

## Notes

- ADR-0554 (Decided by Claude under Stewart's delegation): internal `LastResult` on `ManagedEasyTransfer` holds the last perform's whole `TransferResult` (from options or a `ToolAttempt`; `null` before one, after a no-URL or malformed-URL perform and after a progress abort). A property, not a new return type, so `IEasyTransfer` stays libcurl-shaped.
- Internal `ToolInputs` (`EngineToolTransferInputs`: proxy, looked-up credentials, SSH options, `If-None-Match` lines, range, resume offset, form body, upload, alt-svc cache) replaces the handle's own choices whole when set, nulls included. Sensible default taken: setting it also turns off the handle's cookie, alt-svc and HSTS file setup and cleanup (one switch, as the runner does all three in one place); the HTTP handler's cookie store stays the engine's shared one, and handing the runner's cookie engine over is BL-2394's wiring.
- `Duplicate` does not copy `ToolInputs` and `Reset` clears it (per-transfer state).
- Tests: `ManagedEasyTransferTests.ToolInputs.cs` (15 tests), each input compared against a direct `TransferContextFactory.Create` call with the same pieces. Quality: `PerformFromOptionsAsync` first measured at complexity 12; `TransferUrlOf` and `InputsOf` extracted, re-measured 0 failing members.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. ManagedEasyTransfer keeps its last perform's whole TransferResult (LastResult) and takes the runner's chosen proxy, credentials, SSH options, If-None-Match lines, range, offset, form body, upload and alt-svc cache (ToolInputs), ADR-0554
