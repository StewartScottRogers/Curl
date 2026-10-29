---
id: BL-4171
title: Pass extension command-line options from Curl.Console to the parser and to transfers
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4088, BL-4092]
touches: [Curl.Console/CurlCommandRunner.cs, Curl.Console/CurlComposition.cs, Curl.Console.UnitTests/ExtensionCommandLineOptionTests.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4171 — Pass extension command-line options from Curl.Console to the parser and to transfers

## Goal

`curl --<extension-option>` given on the command line reaches the extension at transfer time, and with `CURL_EXTENSIONS=0` is refused as curl refuses an unknown option.

## Context

BL-4092 built the pieces (ADR-1006): `ExtensionCommandLineOptions.For(readEnvironmentVariable, CommandLineParser.IsCurlLongOption)` in `Curl.Extension.Composition`, the `CommandLineParser.Parse` overload that takes `IReadOnlyList<CommandLineExtensionOption>`, `CommandLineOptions.ExtensionOptionValues`, and `ExtensionOptionValues` in `Curl.Extension.Abstractions`. The Console's parse call is in `Curl.Console/CurlCommandRunner.cs` (about line 1512), which BL-4088 held when BL-4092 ran.

## Acceptance criteria

- [x] `CurlCommandRunner` maps each `ExtensionOption` from `ExtensionCommandLineOptions.For` to a `CommandLineExtensionOption` (`TakesValue` for `ExtensionOptionArgument.Value`) and passes them to `CommandLineParser.Parse`.
- [x] Each option group's `ExtensionOptionValues` is built from `CommandLineOptions.ExtensionOptionValues` and is reachable where an extension's transfer hook will read it.
- [x] A Console unit test registers a test extension option and shows it parsed and read; another shows it refused with exit 2 and curl's unknown-option bytes under `CURL_EXTENSIONS=0`.
- [x] Every existing `Curl.Console.UnitTests` test passes unchanged; `dotnet build` is clean and the fast tests pass; Curl.Console meets the quality gates.

## Notes

- `CurlCommandRunner.ParsedExtensionOptions` gives the parser none under `CURL_EXTENSIONS=0`, otherwise the runner's new optional `extensionOptions` parameter, else `ExtensionCommandLineOptions.For(EnvironmentVariables, CommandLineParser.IsCurlLongOption)`, each mapped to a `CommandLineExtensionOption` (`TakesValue` for `ExtensionOptionArgument.Value`). Decided (sensible default): the parameter lets the Console test register its own options, since `ExtensionOptionCatalogue` is internal and still empty; the runner checks the switch itself so test-given options are refused under `CURL_EXTENSIONS=0` too. The delegates are static readonly fields made with `new`, as the runner's `NoEnvironmentVariables` is, so no method-group cache branch makes coverage order-dependent (BL-4083's cause).
- `CurlComposition.ExtensionOptionValuesOf(CommandLineOptions)` builds the group's `ExtensionOptionValues`; it is reachable in every `createTransferDispatch(options)` call, which is where BL-4173 will pass `TransferHooks.ActivatedBy(values, catalogue).Around` to the engine. The Console test reads it there.
- Measured: Curl.Console 100% line; the one failing member is the pre-existing `CurlComposition.CreateTransferDispatch(CurlTransports, ...)` branch (83.33%) that BL-4083 (Backlog) already names, untouched here. Curl.Console.UnitTests: 3053 passed, 33 skipped; whole-solution fast tests: 50 projects passed, none failed. The tests are platform-neutral (in-memory file system, `file:///x`, no OS text).

## Log

- 2026-10-29: Created (from BL-4092).
- 2026-10-29: Backlog -> Doing. Criteria at claim: 092399cc 5c185e4f f77def69 dc71d5d9.
- 2026-10-29: Doing -> Done. Extension command-line options reach the parser and each option group's ExtensionOptionValues; refused as unknown under CURL_EXTENSIONS=0
