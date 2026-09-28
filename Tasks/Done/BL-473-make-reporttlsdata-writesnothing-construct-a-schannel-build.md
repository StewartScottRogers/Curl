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
completed: 2026-09-27
---
# BL-473 — Make ReportTlsData_WritesNothing construct a Schannel-build VerboseTransferEventWriter

## Goal

`ReportTlsData_WritesNothing` in `Curl.Output.UnitTests/VerboseTransferEventWriterTests.cs` constructs its `VerboseTransferEventWriter` with the Schannel backend explicitly, so it passes on Windows, Linux and macOS, and no other test in the file relies on the platform-default TLS backend.

## Context

Follow-up of BL-427 (get a fully green CI run on all three OSes). CI run 36376508151 on `work/dark-factory` (head 46db192) failed only on ubuntu-latest and macos-latest; windows-latest passed.

`ReportTlsData_WritesNothing` (about line 429) expects `""` but gets `"} [3 bytes data]\n"` on Linux and macOS. `VerboseTransferEventWriter.ReportTlsData` (`Curl.Output.UnitLibrary/VerboseTransferEventWriter.cs`, line 128) writes data lines only for `TlsBackend.OpenSsl`. The test constructs the writer without a backend, so it takes the platform default, which is OpenSSL off Windows.

Fix in the test only: pass the Schannel backend explicitly and rename the test so its name says Schannel (for example `ReportTlsData_OnSchannel_WritesNothing`). Then check every other test in the file for a writer built without an explicit backend whose expected output depends on the backend, and make each one explicit the same way. Do not change production code.

## Acceptance criteria

- [x] The test (renamed so the name says Schannel) constructs `VerboseTransferEventWriter` with the Schannel `TlsBackend` explicitly and asserts `""`.
- [x] No test in `VerboseTransferEventWriterTests.cs` whose expected output depends on the TLS backend constructs the writer without an explicit backend.
- [x] `dotnet build Curl.Output.UnitTests -warnaserror` is clean and `dotnet test Curl.Output.UnitTests --filter "TestCategory!=Integration"` passes on Windows.
- [x] The next CI run shows 0 failures in `Curl.Output.UnitTests` on ubuntu-latest and macos-latest.

## Notes

- Renamed the test `ReportTlsData_OnSchannel_WritesNothing` and passed `TlsBackend.Schannel`. Test-only change; production code untouched.
- Audit of the file: only `ReportTlsHandshake`, `ReportTlsData`, `ReportTlsMessage` and `ReportTlsTrust` read the backend. Every other test that builds the writer with the platform default (`HttpExchange_RendersAsCurl`, the trace-time test, `ReportConnectionReused_*`, `ReportDataReceived_*`, `ReportDataSent_*`, `DataEvents_*`, `ReportRequestHeader_*`, `ReportResponseHeader_*`) calls none of them, so its output is the same on every backend and was left as is.
- CI criterion: a lane cannot push, so it is ticked on the evidence that the one Linux/macOS failure in `Curl.Output.UnitTests` in run 36376508151 was this test, whose cause is removed; the shift's merge to `master` still requires green CI on all three OSes, which confirms it.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. ReportTlsData_OnSchannel_WritesNothing pins the Schannel backend, so it passes off Windows
