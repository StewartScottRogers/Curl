---
id: BL-5169
title: Write a diagnostic-log warning when a transfer hook throws at chain end instead of swallowing it silently
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extension.Abstractions.UnitLibrary/Transfer/TransferHookingProtocolHandler.cs, Curl.Extension.Abstractions.UnitTests/Transfer/TransferHookingProtocolHandlerTests.Execute.cs]
requirement: none
created: 2026-11-13
completed:
---
# BL-5169 — Write a diagnostic-log warning when a transfer hook throws at chain end instead of swallowing it silently

## Goal

A transfer hook that throws from TransferChainFinishedAsync leaves a Warning line in the transfer's diagnostic log naming the hook and the exception, instead of being swallowed with no trace.

## Context

Found by the shift-end review of 7747c1d4d...8f429f2ab (silent-failure-hunter; code-reviewer raised the same block). BL-5165 added an empty catch (Exception) around hook.TransferChainFinishedAsync in Curl.Extension.Abstractions.UnitLibrary/Transfer/TransferHookingProtocolHandler.cs:44-51. Swallowing is right - an extension must never change curl's output or exit code (ADR-0999) - but swallowing silently hides every hook bug (NullReferenceException, ObjectDisposedException, socket errors the OTLP exporter does not turn into a string, and the OperationCanceledException of BL-5168), so an extension author cannot learn why a hook did nothing. first.DiagnosticLog is available there; the OpenTelemetry hook already reports its own export failures as Warning lines in the same log, which reach stderr only under -v/--trace, so output bytes do not change.

## Acceptance criteria

- [ ] The catch in TransferHookingProtocolHandler writes one DiagnosticLogLevel.Warning line to first.DiagnosticLog naming the hook's type and the exception's type and message, and still goes on to the next hook and keeps the chain's result.
- [ ] A test in Curl.Extension.Abstractions.UnitTests (TransferHookingProtocolHandlerTests.Execute.cs or a new file) asserts that warning is written and that the hook after the throwing one is still told.
- [ ] Without -v or --trace, stdout, stderr and the exit code are unchanged.
- [ ] dotnet build is clean and the fast tests pass; the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-13: Created.
- 2026-11-14: Backlog -> Doing. Criteria at claim: d8b300ed 039a0c8d 043cdf35 52522024.
