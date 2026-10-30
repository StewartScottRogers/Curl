---
id: BL-4327
title: Let extensions add -w write-out variables
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary/TransferReport.cs, Curl.Output.UnitLibrary/TransferWriteOutVariables.cs, Curl.Output.UnitTests/TransferWriteOutVariablesTests.cs, Curl.Protocol.Abstractions.UnitTests/TransferReportTests.cs, Curl.Extension.Abstractions.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-29
completed:
---
# BL-4327 — Let extensions add -w write-out variables

## Goal

An extension's transfer hook can give a transfer named write-out values that `-w` prints as `%{name}`, without changing any variable curl itself has.

## Context

Found by BL-4140 (Structured Field Values extension, ADR-0999), whose option must show parsed structured fields "in verbose output and `-w`". Verbose output is already reachable (`ITransferContext.Events.ReportInfo`), but no hook can add a `-w` variable: `Curl.Output.UnitLibrary/TransferWriteOutVariables.cs` resolves every variable from `TransferResult.Report` (`Curl.Protocol.Abstractions.UnitLibrary/TransferReport.cs`), and a hook's `ITransferHook.ExecuteAsync` already returns the `TransferResult` the write-out reads. Suggested shape: a `TransferReport.ExtensionWriteOutValues` (name to text, empty by default) that `TransferWriteOutVariables` consults only for a name curl does not know, so a curl variable can never be shadowed and, with no extension active, every `-w` output is byte-for-byte unchanged. Record the decision in an ADR marked "Decided by Claude under Stewart's delegation".

## Acceptance criteria

- [ ] A transfer hook can return a `TransferResult` whose report carries named write-out values, and `-w '%{<name>}'` prints a value given under that name; unit tests.
- [ ] A name curl already knows always prints curl's value, and an unknown name with no extension value is reported exactly as before; unit tests.
- [ ] `Curl.Extension.Abstractions.UnitLibrary/CLAUDE.md` says how a hook supplies write-out values.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records the design.
- [ ] The changed libraries meet the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
