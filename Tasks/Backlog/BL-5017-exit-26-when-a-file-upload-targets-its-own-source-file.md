---
id: BL-5017
title: Exit 26 when a file:// upload targets its own source file
priority: High
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Original.Protocol.File.UnitLibrary/FileProtocolHandler.cs, Curl.Original.Protocol.File.UnitTests/FileUploadOntoOwnSourceTests.cs]
requirement: none
created: 2026-11-12
completed:
---
# BL-5017 — Exit 26 when a file:// upload targets its own source file

## Goal

A `file://` upload whose destination is its own source file fails with `curl: (26) client read function EOF fail, only 0/6 of needed bytes read` and exit 26, instead of emptying the file and exiting 0.

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `preview` curl-win-x64.zip; REF = curl 8.22.0 Schannel, REF2 = curl 8.18.0 OpenSSL), finding STD-038 (Low, Confirmed). Silent wrong operation: the source is destroyed and success is reported (REF destroys it too, but fails loudly).

- **Repro:**
  - `printf 'HELLO\n' > s.txt; curl -sS -T s.txt file:///<dir>/s.txt; echo "exit $?"; od -c s.txt`
  - `printf 'HELLO\n' > s.txt; curl -sS -T s.txt file:///<dir>/; echo "exit $?"; od -c s.txt` (a URL ending in `/` makes curl append the file name, so the target is the same file).
- **Expected (REF):** `curl: (26) client read function EOF fail, only 0/6 of needed bytes read` and exit 26, in both cases.
- **Actual (PORT):** no stderr and exit 0 in both cases; the file is left empty (0 bytes).
- **Where to start:** `Curl.Original.Protocol.File.UnitLibrary/FileProtocolHandler.cs`, `UploadIntoAsync`. It computes `needed = RemainingLength(upload)` after `UploadIntoDestinationAsync` (about line 857) has opened the destination with `FileWriteMode.Truncate`. Capture the source length before truncating (reproducing REF's `0/6` text), or refuse when source and destination are the same file.

## Acceptance criteria

- [ ] A new test class `Curl.Original.Protocol.File.UnitTests/FileUploadOntoOwnSourceTests.cs` asserts that uploading a 6-byte source to its own path returns the failure that maps to `CurlExitCode` 26 with the message `client read function EOF fail, only 0/6 of needed bytes read`.
- [ ] The same holds when the destination URL ends in `/` so the file name is appended and the target is the source.
- [ ] Regression: uploading a source to a different destination still copies it and succeeds, and an upload onto an existing different file still truncates and replaces it.
- [ ] Tests use drive-less `file:///dir/x` URLs or the handler's injected file abstractions, so they pass on Windows, Linux and macOS.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text. BL-5025 also edits `FileProtocolHandler.cs`; the board will not run them together.

## Log

- 2026-11-12: Created.
