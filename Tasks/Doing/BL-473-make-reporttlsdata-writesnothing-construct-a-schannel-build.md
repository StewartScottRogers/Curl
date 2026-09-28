---
id: BL-473
title: Make ReportTlsData_WritesNothing construct a Schannel-build VerboseTransferEventWriter
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Output.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-473 — Make ReportTlsData_WritesNothing construct a Schannel-build VerboseTransferEventWriter

## Goal

`ReportTlsData_WritesNothing` in `Curl.Output.UnitTests/VerboseTransferEventWriterTests.cs` constructs its `VerboseTransferEventWriter` with the Schannel backend explicitly, so it passes on Windows, Linux and macOS, and no other test in the file relies on the platform-default TLS backend.

## Context

Follow-up of BL-427 (get a fully green CI run on all three OSes). CI run 36376508151 on `work/dark-factory` (head 46db192) failed only on ubuntu-latest and macos-latest; windows-latest passed.

`ReportTlsData_WritesNothing` (about line 429) expects `""` but gets `"} [3 bytes data]\n"` on Linux and macOS. `VerboseTransferEventWriter.ReportTlsData` (`Curl.Output.UnitLibrary/VerboseTransferEventWriter.cs`, line 128) writes data lines only for `TlsBackend.OpenSsl`. The test constructs the writer without a backend, so it takes the platform default, which is OpenSSL off Windows.

Fix in the test only: pass the Schannel backend explicitly and rename the test so its name says Schannel (for example `ReportTlsData_OnSchannel_WritesNothing`). Then check every other test in the file for a writer built without an explicit backend whose expected output depends on the backend, and make each one explicit the same way. Do not change production code.

## Acceptance criteria

- [ ] The test (renamed so the name says Schannel) constructs `VerboseTransferEventWriter` with the Schannel `TlsBackend` explicitly and asserts `""`.
- [ ] No test in `VerboseTransferEventWriterTests.cs` whose expected output depends on the TLS backend constructs the writer without an explicit backend.
- [ ] `dotnet build Curl.Output.UnitTests -warnaserror` is clean and `dotnet test Curl.Output.UnitTests --filter "TestCategory!=Integration"` passes on Windows.
- [ ] The next CI run shows 0 failures in `Curl.Output.UnitTests` on ubuntu-latest and macos-latest.

## Notes

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
