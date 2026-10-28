---
id: BL-3984
title: Add ITransferEvents.ReportWarning that the console writes as a Warning: line on stderr
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary/ITransferEvents.cs, Curl.Console/ConnectionIdRecordingTransferEvents.cs, Curl.Console/MultiStateTraceEvents.cs, Curl.Console/OutputFileOpenFailureTransferEvents.cs, Curl.Console/ResponseCodeRecordingTransferEvents.cs, Curl.Console/TlsResultRecordingTransferEvents.cs, Curl.Console/TransferEventOutput.cs, Curl.Console.UnitTests/TransferEventWarningTests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-3984 — Add ITransferEvents.ReportWarning that the console writes as a Warning: line on stderr

## Goal

A library under the console can report a warning that curl prints without `-v` (`Warning: <text>` on stderr) through `ITransferEvents.ReportWarning`.

## Context

Needed by BL-3714 (CGR-185): a refused `SO_PRIORITY` must print `Warning: VLAN priority 7 failed with errno 1: Operation not permitted` from `Curl.Networking.UnitLibrary`'s `TcpDialer`, which has only `ITransferEvents` (whose `ReportInfo` is `-v` only). Add `void ReportWarning(string text)` to `ITransferEvents` with a default no-op body (like `ReportTlsTrust`), forward it in every Console wrapper that forwards `ReportInfo` (the five files in `touches`), and have `TransferEventOutput` write `Warning: <text>` plus newline to stderr as the console's other warnings do (see `OutputFileOpenWarning`). Check `Curl.Engine.UnitLibrary` and `Libcurl` wrappers of `ITransferEvents` too; only add a forward where one already forwards every member.

## Acceptance criteria

- [ ] `ITransferEvents.ReportWarning(string)` exists with a default no-op body, and each Console wrapper that forwards `ReportInfo` forwards it.
- [ ] A focused test in `Curl.Console.UnitTests/TransferEventWarningTests.cs` shows `TransferEventOutput` writes `Warning: <text>` on stderr without `-v`.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes; Curl.Console and Abstractions stay at their quality gates.

## Notes

## Log

- 2026-10-28: Created.
