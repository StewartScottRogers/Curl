---
id: BL-4164
title: Pass extension command-line options from Curl.Console to the parser and to transfers
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4088, BL-4092]
touches: [Curl.Console/CurlCommandRunner.cs, Curl.Console/CurlComposition.cs, Curl.Console.UnitTests/ExtensionCommandLineOptionTests.cs]
requirement: none
created: 2026-10-29
completed:
---
# BL-4164 — Pass extension command-line options from Curl.Console to the parser and to transfers

## Goal

`curl --<extension-option>` given on the command line reaches the extension at transfer time, and with `CURL_EXTENSIONS=0` is refused as curl refuses an unknown option.

## Context

BL-4092 built the pieces (ADR-1004): `ExtensionCommandLineOptions.For(readEnvironmentVariable, CommandLineParser.IsCurlLongOption)` in `Curl.Extension.Composition`, the `CommandLineParser.Parse` overload that takes `IReadOnlyList<CommandLineExtensionOption>`, `CommandLineOptions.ExtensionOptionValues`, and `ExtensionOptionValues` in `Curl.Extension.Abstractions`. The Console's parse call is in `Curl.Console/CurlCommandRunner.cs` (about line 1512), which BL-4088 held when BL-4092 ran.

## Acceptance criteria

- [ ] `CurlCommandRunner` maps each `ExtensionOption` from `ExtensionCommandLineOptions.For` to a `CommandLineExtensionOption` (`TakesValue` for `ExtensionOptionArgument.Value`) and passes them to `CommandLineParser.Parse`.
- [ ] Each option group's `ExtensionOptionValues` is built from `CommandLineOptions.ExtensionOptionValues` and is reachable where an extension's transfer hook will read it.
- [ ] A Console unit test registers a test extension option and shows it parsed and read; another shows it refused with exit 2 and curl's unknown-option bytes under `CURL_EXTENSIONS=0`.
- [ ] Every existing `Curl.Console.UnitTests` test passes unchanged; `dotnet build` is clean and the fast tests pass; Curl.Console meets the quality gates.

## Notes

## Log

- 2026-10-29: Created (from BL-4092).
