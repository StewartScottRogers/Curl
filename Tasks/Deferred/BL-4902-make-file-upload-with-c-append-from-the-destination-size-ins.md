---
id: BL-4902
title: Make file:// upload with -C - append from the destination size instead of overwriting it
priority: High
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Original.Protocol.File.UnitLibrary/FileProtocolHandler.cs, Curl.Original.Protocol.File.UnitTests/FileUploadResumeUnknownOffsetTests.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4902 — Make file:// upload with -C - append from the destination size instead of overwriting it

## Goal

`-T src -C - file:///dst` behaves like curl's `file_upload`: offset = destination size, the rest of the source appended, nothing truncated.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding STD-006 (Medium (data loss), confirmed: reproduced at least twice).

- Repro: `printf "UPLOAD\n" > src.txt; printf ZZ > dst; curl -sS -C - -T src.txt file:///<abs>/dst; od -c dst`. Repeat with `dst` = `ZZZZZZZZZZZ` (11 bytes).
- Expected: REF 8.22.0: exit 0; first case leaves `ZZLOAD\n`, second leaves `ZZZZZZZZZZZ` unchanged.
- Actual: Exit 0, destination truncated and rewritten as `UPLOAD\n` in both cases (stderr matches REF).
- Where to start: `FileProtocolHandler.UploadIntoDestinationAsync` / `UploadIntoAsync`. `-C -` arrives as `ResumeFrom = null` with `ResumeUploadFromUnknownOffset = true` (set in `Curl.Engine.UnitLibrary/TransferContextFactory.cs`); the File handler never reads that flag (FTP, SFTP, HTTP do).

## Acceptance criteria

- [ ] `dst` = `ZZ` ends as `ZZLOAD\n`, exit 0.
- [ ] `dst` = `ZZZZZZZZZZZ` is unchanged, exit 0.
- [ ] A unit test in `Curl.Original.Protocol.File.UnitTests` pins both cases.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: c912ec10 47b56d31 903bc26c a0b49a29.
- 2026-11-11: Doing -> Backlog. Lane 1 could not integrate: build failed, and still fast tests failed twice (Curl.Console.UnitTests: RunAsync_WindowsProductionRoute_SendsSspisType1AndType3AsCurl8210Measured, RunAsync_WindowsProductionRoute_SendsSspisType1AndAType3AsCurl8210Measured; then Curl.Console.UnitTests: RunAsync_WindowsProductionRoute_SendsSspisType1AndType3AsCurl8210Measured, RunAsync_WindowsProductionRoute_SendsSspisType1AndAType3AsCurl8210Measured) after a repair run after rebasing onto the other lanes' work. The work is on branch factory/BL-4902-lane-1-20261111-082724; start with git cherry-pick --no-commit factory/BL-4902-lane-1-20261111-082724 and fix it.
- 2026-11-11: Backlog -> Deferred. Split into BL-4972 and BL-4973
