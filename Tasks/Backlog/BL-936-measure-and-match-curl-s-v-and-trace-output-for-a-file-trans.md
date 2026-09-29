---
id: BL-936
title: Measure and match curl's -v and --trace output for a file:// transfer
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-936 — Measure and match curl's -v and --trace output for a file:// transfer

## Goal

A `file://` transfer's `-v`, `--trace` and `--trace-ascii` output is measured against curl 8.21.0 and matched byte for byte, for a download, a `-T` upload, `-I`, and a missing file.

## Context

- Audit 2026-09-29, part B: `FileProtocolHandler.cs` reports only the header-write failure line (line ~741) through `context.Events`; no task on the board or in the archive records curl's `-v` or `--trace` output for `file://` as measured, so whether curl writes further info lines or data blocks for it is unverified.
- Where: `Curl.Protocol.File.UnitLibrary/FileProtocolHandler.cs` and `FileTransferMessages` (keep measured texts there).
- Measure first with `Record-CurlExchange.ps1 -NoServer` (real curl, no server): `-v`, `--trace-ascii -` and `--trace -` for `file:///<tempdir>/a.txt` download, `-T a.txt file:///<tempdir>/b.txt`, `-I file:///<tempdir>/a.txt`, and a missing file (exit 37). Copy the output into Notes with curl's version and build. If curl writes nothing beyond what Curl already writes, the task's result is tests pinning that, not new code.
- Tests must pass on Linux and macOS: use drive-less `file:///dir/x` URLs over the injected `IFileSystem`, never a drive letter (root `CLAUDE.md`).

## Acceptance criteria

- [ ] Measured output for the four cases is copied into Notes.
- [ ] `Curl.Protocol.File.UnitTests` pin, through a recording `ITransferEvents`, exactly the measured events for the four cases (including, where curl writes none, that none are reported).
- [ ] Existing tests pass unmodified.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Protocol.File.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
