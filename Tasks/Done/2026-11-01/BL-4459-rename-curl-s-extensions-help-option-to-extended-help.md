---
id: BL-4459
title: Rename Curl's --extensions-help option to --extended-help
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Cli.UnitLibrary/CommandLineOptions.cs, Curl.Cli.UnitLibrary/CommandLineGlobalState.cs, Curl.Cli.UnitLibrary/CurlAiHelpText.cs, Curl.Mcp.UnitLibrary/McpOptionPathTable.cs, Curl.Extension.Composition.UnitLibrary/ExtensionListing.cs, Curl.Console.UnitTests/CurlCommandRunnerExtensionsTests.cs, Curl.Console/CurlCommandRunner.cs, Curl.Cli.UnitTests/LibcurlSourceCodeOptionCoverageTests.cs, Curl.Mcp.UnitLibrary/McpVersionToolResult.cs, Curl.Mcp.IntegrationTests/Golden/resources-read.json]
requirement: none
created: 2026-11-01
completed: 2026-11-01
---
# BL-4459 — Rename Curl's --extensions-help option to --extended-help

## Goal

`curl --extended-help` prints the extension listing that `curl --extensions-help` prints today, and `--extensions-help` is gone: curl answers it as any unknown option (exit 2).

## Context

Stewart, 2026-11-01: "rename the command line arg `--extensions-help` to `--extended-help`". BL-4452 (Done, `edd1261c9`) renamed `--extensions` to `--extensions-help` earlier the same day; follow what it changed.

On 2026-11-01 `extensions-help` / `ExtensionsHelp` appeared 100 times in 85 files: the option row and name list in `CommandLineOptionTable.cs`, `CommandLineOptions.ExtensionsHelpRequested` / `CommandLineGlobalState` (rename to `ExtendedHelpRequested`), `CurlCommandRunner` in `Curl.Console`, `CurlAiHelpText.cs` (its Curl-category row and index line), `McpOptionPathTable.cs` (the MCP `curl` tool's option list), `McpVersionToolResult.cs`, `ExtensionListing.cs`, `CurlCommandRunnerExtensionsTests.cs`, `LibcurlSourceCodeOptionCoverageTests.cs`, the golden `Curl.Mcp.IntegrationTests/Golden/resources-read.json`, ADR-1008, several CLAUDE.md files, and one doc comment in each extension's `Description` (about 60 `*Extension.cs` / `*Options.cs` files). Those doc-comment lines are one-word edits each; they are not listed in `touches` and need no other change. Find them with `rg -l "extensions-help|ExtensionsHelp"`.

Keep the names that are not the option: the `--ai-help extensions` subject and the MCP resource `curl://ai-help/extensions`. Check `ExtensionCommandLineOptions`' clash rule (no curl option or extension option is named `extended-help`). `--help` still leaves it out (ADR-1008).

## Acceptance criteria

- [x] `curl --extended-help` prints exactly what `curl --extensions-help` printed before (and the off line under `CURL_EXTENSIONS=0`); `curl --extensions-help` now fails as an unknown option with exit 2; unit tests pin both.
- [x] `rg "extensions-help|ExtensionsHelp"` finds nothing outside history (ADR amendment and log lines, Done tasks).
- [x] `--ai-help` names `--extended-help`, and the MCP `curl` tool accepts `--extended-help` (`McpOptionPathTable`); the golden MCP files are re-recorded if they change.
- [x] ADR-1008 carries an amendment for this rename.
- [x] `dotnet build` and the fast tests pass; the touched libraries stay at 100% line and branch coverage.

## Notes

- Renamed `extensions-help` to `extended-help` and `ExtensionsHelp` to `ExtendedHelp` in every file outside Tasks and ADR-1008 (84 files, 99 lines; the extension doc comments are one-word edits). Widened `touches` with the files the Context names that the list left out (CurlCommandRunner.cs, LibcurlSourceCodeOptionCoverageTests.cs, McpVersionToolResult.cs, the MCP golden resources-read.json); no task in Doing on work/dark-factory names them.
- The only `extensions-help` left outside history is the DataRow in `CurlCommandRunnerExtensionsTests.RunAsync_FormerExtensionsOptionName_FailsAsAnUnknownOption`, which criterion 1 requires to pin the old name as unknown (exit 2), beside the bare `--extensions`.
- The golden `resources-read.json` changed only in the `--ai-help` text, the same rename, so it was updated in place rather than re-recorded.
- Checked with the built curl.exe: `--extended-help` exits 0 with the listing, `CURL_EXTENSIONS=0` prints the off line, `--extensions-help` prints `curl: option --extensions-help: is unknown` and exits 2. ADR-1008 carries the BL-4459 amendment.
- dotnet build clean; fast tests green (Cli 4501, Console 3119, Mcp 547, Composition 31 passed; whole solution no failures). Measure-CodeQuality for Curl.Cli.UnitLibrary, Curl.Console, Curl.Mcp.UnitLibrary, Curl.Extension.Composition.UnitLibrary: 0 failing members.
- Criterion about the ADR met by the amendment; CI on Linux and macOS is covered by keeping the change platform-neutral (string rename only).

## Log

- 2026-11-01: Created.
- 2026-11-01: Backlog -> Doing. Criteria at claim: 1683e6c0 9700a053 65ac83db bcd3f8ca 91fd3925.
- 2026-11-01: Doing -> Done. curl --extended-help prints the extension listing; --extensions-help is an unknown option (exit 2)
