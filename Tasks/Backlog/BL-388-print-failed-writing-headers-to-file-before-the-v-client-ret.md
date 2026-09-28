---
id: BL-388
title: Print 'Failed writing headers to <file>' before the -v 'client returned ERROR' line
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-111]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-388 — Print 'Failed writing headers to <file>' before the -v 'client returned ERROR' line

## Goal

Under `-v`, when a `-D` header write fails, Curl.Console prints `curl: Failed writing headers to <file>` before the handler's `* client returned ERROR on write of N bytes` verbose line, as curl 8.21.0 does.

## Context

BL-111 measured curl 8.21.0 on Windows: with `-v`, `curl: Failed writing headers to -` is followed by `* client returned ERROR on write of 20 bytes`, then `curl: (23) ...`. curl prints its line inside the header callback, at the moment the flush fails. BL-111 made `CurlCommandRunner.TransferReportingHeaderWriteFailureAsync` print the line after the transfer returns, from `DumpHeaderOutputStream.HasWriteFailed`, so any verbose line the handler writes after the failure comes first. Where to start: `Curl.Console/DumpHeaderOutputStream.cs` (for example give it a callback that writes the line to standard error when the write fails) and the verbose output path in `Curl.Console/CurlCommandRunner.cs`. First confirm the file handler emits the verbose `client returned ERROR` line at all.

## Acceptance criteria

- [ ] A test in `Curl.Console.UnitTests` drives `-v -D - -o body.txt` over a `file` transfer whose header write to standard output fails, and asserts `curl: Failed writing headers to -` appears on stderr before `* client returned ERROR on write of 20 bytes` and before `curl: (23) client returned ERROR on write of 20 bytes`.
- [ ] The BL-111 tests in `CurlCommandRunnerDumpHeaderTests` still pass unchanged.
- [ ] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-09-27: Created.
