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
completed: 2026-10-29
---
# BL-4327 — Let extensions add -w write-out variables

## Goal

An extension's transfer hook can give a transfer named write-out values that `-w` prints as `%{name}`, without changing any variable curl itself has.

## Context

Found by BL-4140 (Structured Field Values extension, ADR-0999), whose option must show parsed structured fields "in verbose output and `-w`". Verbose output is already reachable (`ITransferContext.Events.ReportInfo`), but no hook can add a `-w` variable: `Curl.Output.UnitLibrary/TransferWriteOutVariables.cs` resolves every variable from `TransferResult.Report` (`Curl.Protocol.Abstractions.UnitLibrary/TransferReport.cs`), and a hook's `ITransferHook.ExecuteAsync` already returns the `TransferResult` the write-out reads. Suggested shape: a `TransferReport.ExtensionWriteOutValues` (name to text, empty by default) that `TransferWriteOutVariables` consults only for a name curl does not know, so a curl variable can never be shadowed and, with no extension active, every `-w` output is byte-for-byte unchanged. Record the decision in an ADR marked "Decided by Claude under Stewart's delegation".

## Acceptance criteria

- [x] A transfer hook can return a `TransferResult` whose report carries named write-out values, and `-w '%{<name>}'` prints a value given under that name; unit tests.
- [x] A name curl already knows always prints curl's value, and an unknown name with no extension value is reported exactly as before; unit tests.
- [x] `Curl.Extension.Abstractions.UnitLibrary/CLAUDE.md` says how a hook supplies write-out values.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records the design.
- [x] The changed libraries meet the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Pipeline `feature` done directly: the change is one property and one fallback lookup, so it needed no separate planning stage. The design is in ADR-1115: `TransferReport.ExtensionWriteOutValues` (empty by default), which `TransferWriteOutVariables.TryGetVariableText` checks only after every curl name has missed. The values are not added to `%{json}`, so curl's JSON stays identical.
- Tests: `TransferWriteOutVariablesTests` (value printed, curl names including json and header_json not shadowed, unknown name still unknown) and `TransferReportTests` (empty default, carried).
- `Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary,Curl.Output.UnitLibrary`: 0 failing members. `dotnet build` is clean and the fast tests are green on Windows. The change is platform-neutral (no paths, OS text or certificates), so CI on Linux and macOS should stay green.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 15c78d37 b2841475 bd0dc039 21c21cdc 478f867b 3d836d5e.
- 2026-10-29: Doing -> Done. Extension transfer hooks add -w variables through TransferReport.ExtensionWriteOutValues, never shadowing curl's
