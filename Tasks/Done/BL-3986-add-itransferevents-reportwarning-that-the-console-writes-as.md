---
id: BL-3986
title: Add ITransferEvents.ReportWarning that the console writes as a Warning: line on stderr
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary/ITransferEvents.cs, Curl.Console/ConnectionIdRecordingTransferEvents.cs, Curl.Console/MultiStateTraceEvents.cs, Curl.Console/OutputFileOpenFailureTransferEvents.cs, Curl.Console/ResponseCodeRecordingTransferEvents.cs, Curl.Console/TlsResultRecordingTransferEvents.cs, Curl.Console/TransferEventOutput.cs, Curl.Console.UnitTests/TransferEventWarningTests.cs, Curl.Console/WarningWritingTransferEvents.cs, Curl.Console.UnitTests/CurlCommandRunnerTransferEventTests.cs, Curl.Protocol.Abstractions.UnitTests/ITransferEventsTests.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-3986 — Add ITransferEvents.ReportWarning that the console writes as a Warning: line on stderr

## Goal

A library under the console can report a warning that curl prints without `-v` (`Warning: <text>` on stderr) through `ITransferEvents.ReportWarning`.

## Context

Needed by BL-3714 (CGR-185): a refused `SO_PRIORITY` must print `Warning: VLAN priority 7 failed with errno 1: Operation not permitted` from `Curl.Networking.UnitLibrary`'s `TcpDialer`, which has only `ITransferEvents` (whose `ReportInfo` is `-v` only). Add `void ReportWarning(string text)` to `ITransferEvents` with a default no-op body (like `ReportTlsTrust`), forward it in every Console wrapper that forwards `ReportInfo` (the five files in `touches`), and have `TransferEventOutput` write `Warning: <text>` plus newline to stderr as the console's other warnings do (see `OutputFileOpenWarning`). Check `Curl.Engine.UnitLibrary` and `Libcurl` wrappers of `ITransferEvents` too; only add a forward where one already forwards every member.

## Acceptance criteria

- [x] `ITransferEvents.ReportWarning(string)` exists with a default no-op body, and each Console wrapper that forwards `ReportInfo` forwards it.
- [x] A focused test in `Curl.Console.UnitTests/TransferEventWarningTests.cs` shows `TransferEventOutput` writes `Warning: <text>` on stderr without `-v`.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes; Curl.Console and Abstractions stay at their quality gates.

## Notes

- Warnings are written by a new `WarningWritingTransferEvents` wrapper that `TransferEventOutput.OpenAsync` puts around the run's events unless `-s` is given (curl's `warnf` is silenced only by `-s`), wrapped at the default terminal width as `warnf` wraps. Added it, the runner test that pinned the bare no-op sink, and the Abstractions default-body test to touches; no task in Doing on origin/work/dark-factory names them.
- Nine `Curl.Engine.UnitLibrary` wrappers forward every member (`ReportTlsTrust` included) but are outside this task's touches; their `ReportWarning` forward is filed as BL-3988 so BL-3714's warning reaches the console through the engine.
- Linux/macOS CI: the tests use in-memory streams and pin LF and CR LF via the `runsOnWindows` argument, so they are platform-neutral.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 4fb3633e c4add7fc d418a7e3.
- 2026-10-28: Doing -> Done. ReportWarning added; console writes Warning: lines on stderr unless -s; build clean, fast tests green, quality gates pass
