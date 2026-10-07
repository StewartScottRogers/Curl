---
id: BL-388
title: Print 'Failed writing headers to <file>' before the -v 'client returned ERROR' line
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-111]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-388 — Print 'Failed writing headers to <file>' before the -v 'client returned ERROR' line

## Goal

Under `-v`, when a `-D` header write fails, Curl.Console prints `curl: Failed writing headers to <file>` before the handler's `* client returned ERROR on write of N bytes` verbose line, as curl 8.21.0 does.

## Context

BL-111 measured curl 8.21.0 on Windows: with `-v`, `curl: Failed writing headers to -` is followed by `* client returned ERROR on write of 20 bytes`, then `curl: (23) ...`. curl prints its line inside the header callback, at the moment the flush fails. BL-111 made `CurlCommandRunner.TransferReportingHeaderWriteFailureAsync` print the line after the transfer returns, from `DumpHeaderOutputStream.HasWriteFailed`, so any verbose line the handler writes after the failure comes first. Where to start: `Curl.Console/DumpHeaderOutputStream.cs` (for example give it a callback that writes the line to standard error when the write fails) and the verbose output path in `Curl.Console/CurlCommandRunner.cs`. First confirm the file handler emits the verbose `client returned ERROR` line at all.

## Acceptance criteria

- [x] A test in `Curl.Console.UnitTests` drives `-v -D - -o body.txt` over a `file` transfer whose header write to standard output fails, and asserts `curl: Failed writing headers to -` appears on stderr before `* client returned ERROR on write of 20 bytes` and before `curl: (23) client returned ERROR on write of 20 bytes`.
- [x] The BL-111 tests in `CurlCommandRunnerDumpHeaderTests` still pass unchanged.
- [x] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- 2026-09-27, lane 3: the file handler did not emit the `-v` `* client returned ERROR on write of N bytes` line at all, so `touches` gained `Curl.Protocol.File.UnitLibrary` and `Curl.Protocol.File.UnitTests` (no task in Doing names either). `FileProtocolHandler.WriteHeadersAsync` now reports the failure message through `context.Events.ReportInfo` before returning exit 23, as libcurl's `failf` sends every error to the verbose stream; pinned by `FileProtocolHandlerDecisionTests.ExecuteAsync_HeaderOutputFails_ReportsTheWriteErrorAsAnInformationLine`.
- `DumpHeaderOutputStream` now takes the `-D` value and standard error (null when `-s` without `-S`) and prints `curl: Failed writing headers to <file>` itself inside the failing write, before rethrowing; `HasWriteFailed` and the runner's after-the-transfer print are gone. Pinned by `CurlCommandRunnerDumpHeaderTests.RunAsync_DumpHeaderToFailingStandardOutputUnderVerbose_PrintsFailedWritingHeadersBeforeTheVerboseLine`; the BL-111 tests pass unchanged.
- Scope kept to the header write: the file handler's other failures (body write, open) still report no `-v` line. Not filed; a general "every failure message is also a `*` line" rule is a wider decision for a later task.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. Under -v a failed -D write prints 'curl: Failed writing headers to <file>' before '* client returned ERROR on write of N bytes'
