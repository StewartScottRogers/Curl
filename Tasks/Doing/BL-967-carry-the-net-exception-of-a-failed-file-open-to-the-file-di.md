---
id: BL-967
title: Carry the .NET exception of a failed file open to the file diagnostic log
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-927]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-967 — Carry the .NET exception of a failed file open to the file diagnostic log

## Goal

A `file://` open that fails writes the underlying .NET exception's type and message in the `file` component's `error` line, as ADR-0222 decision 2 asks for `error` lines.

## Context

- BL-927 made `FileProtocolHandler` log `could not open <path> for reading: <FileAccessStatus>` through `FileTransferLog.OpenFailed`. The exception itself never reaches the handler: `PhysicalFileSystem` (`Curl.Core.UnitLibrary\FileSystem\PhysicalFileSystem.cs`) turns it into a `FileAccessStatus` with `FileOpenFailure.StatusFor` and `FileOpenResult.Failed(FileAccessStatus)` carries only the status.
- Add an optional exception (or its type and message) to `FileOpenResult` in `Curl.Protocol.Abstractions.UnitLibrary`, set it in `PhysicalFileSystem`, and append it to `FileTransferLog.OpenFailed`'s message. No `-v`, `--trace`, standard output or exit-code change.

## Acceptance criteria

- [ ] `FileOpenResult.Failed` can carry the exception, and `FileOpenResultTests` pin it round-tripping and `null` by default.
- [ ] `PhysicalFileSystem` sets it on every failed open; a `Curl.Core.UnitTests` test pins it for a missing file.
- [ ] `FileTransferLogTests` pin an `error` line that names the exception type and message when one is carried, and the current line when none is.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `Measure-CodeQuality.ps1 -Library <library>` reports 100% line and branch coverage and no failing member for each library touched.

## Notes

Filed by BL-927, whose `touches` did not include the abstractions or the physical file system.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
