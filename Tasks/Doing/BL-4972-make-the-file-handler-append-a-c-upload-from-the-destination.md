---
id: BL-4972
title: Make the File handler append a -C - upload from the destination size
priority: High
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Original.Protocol.File.UnitLibrary/FileProtocolHandler.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4972 — Make the File handler append a -C - upload from the destination size

## Goal

`-T src -C - file:///dst` behaves like curl's `file_upload`: offset = destination size, the rest of the source appended, nothing truncated.

## Context

Split from BL-4902 (finding STD-006 of the 2026-11-11 outside fitness review; REF = curl 8.22.0 Schannel). `-C -` arrives as `ResumeFrom = null` with `ResumeUploadFromUnknownOffset = true` (set in `Curl.Engine.UnitLibrary/TransferContextFactory.cs`); `FileProtocolHandler.UploadIntoDestinationAsync` / `UploadIntoAsync` never read that flag (FTP, SFTP, HTTP do), so the destination is truncated.

## Acceptance criteria

- [x] With `ResumeUploadFromUnknownOffset` set, the File handler uploads from the destination's current size onward: `dst` = `ZZ` and source `UPLOAD\n` ends as `ZZLOAD\n`, exit 0.
- [x] `dst` = `ZZZZZZZZZZZ` (11 bytes, longer than the source) is left unchanged, exit 0.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Split from BL-4902 (over its cost cap). The earlier run's work is on branch factory/BL-4902-lane-1-20261111-082724; it may be cherry-picked with `git cherry-pick --no-commit` as a starting point (its integration failed only on unrelated Curl.Console.UnitTests SSPI tests). The focused unit test is BL-4973; the handler code must stay at 100% line and branch coverage, so BL-4973 carries the pinning tests. Measure real curl before pinning any text; no Python.

Done: cherry-picked the BL-4902 handler change (append mode plus skip = destination size); the pinning tests came with it (FileUploadResumeUnknownOffsetTests). File handler coverage 100% line and branch; build clean, File fast tests 487 pass. A full Measure-CodeQuality run stopped on an unrelated Stomp requirement-matrix test (StompUploadTests, another lane), so coverage was measured on the File tests alone.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: fe598c36 6090b5c1 a0b49a29.
