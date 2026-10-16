---
id: BL-2362
title: Load the -b files, write the -c jar, alt-svc and HSTS files and number conn_id through ManagedEasyTransfer
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2359]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed:
---
# BL-2362 — Load the -b files, write the -c jar, alt-svc and HSTS files and number conn_id through ManagedEasyTransfer

## Goal

`ManagedEasyTransfer.PerformAsync` does the per-transfer setup and cleanup `CurlCommandRunner` does around its serial attempt today: it loads the `-b` files into the engine's shared `CookieStore` before connecting, writes the `-c` jar, the `--alt-svc` file and the `--hsts` file after the transfer (`TransferPerformer.LoadCookieFilesAsync`, `OpenAltSvcCacheAsync`, `SwitchToHttpsForHstsAsync`, `WriteCookieJarAltSvcAndHstsFilesAsync`), gives each transfer its `%{conn_id}` (`TransferInfo.ConnectionId`) as the runner's `ConnectionIdRecordingTransferEvents` does, and puts a diagnostic log on the context.

## Context

Left by BL-2359 (ADR-0542), which shares cookies, HSTS, pooled connections and `%{xfer_id}` across an engine's handles but reads no cookie, alt-svc or HSTS file. BL-2318 needs these to route `Curl.Console`'s serial transfers through `IEasyTransfer.PerformAsync` byte for byte. The setup steps take an `IFileSystem` and standard input today; decide in an ADR how the engine gets them (an internal hook beside `ManagedEasyTransfer.ToolSettings` is the likely answer). Trace events and progress are BL-2360.

## Acceptance criteria

- [ ] A `ManagedEasyTransferTests` test shows a `-b` file's cookie sent and a received cookie written to the `-c` jar through the easy handle, the same bytes as `TransferPerformer`'s steps give today.
- [ ] A test shows `--alt-svc` and `--hsts` files read before and written after a transfer through the easy handle.
- [ ] A test shows two transfers on one pooled connection getting the same `TransferInfo.ConnectionId` and a new connection the next one.
- [ ] `dotnet build` is clean (warnings are errors).
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).

## Notes

## Log

- 2026-10-15: Created.
