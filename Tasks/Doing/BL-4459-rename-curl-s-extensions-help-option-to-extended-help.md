---
id: BL-4459
title: Rename Curl's --extensions-help option to --extended-help
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Cli.UnitLibrary/CommandLineOptions.cs, Curl.Cli.UnitLibrary/CommandLineGlobalState.cs, Curl.Cli.UnitLibrary/CurlAiHelpText.cs, Curl.Mcp.UnitLibrary/McpOptionPathTable.cs, Curl.Extension.Composition.UnitLibrary/ExtensionListing.cs, Curl.Console.UnitTests/CurlCommandRunnerExtensionsTests.cs]
requirement: none
created: 2026-11-01
completed:
---
# BL-4459 — Rename Curl's --extensions-help option to --extended-help

## Goal

`curl --extended-help` prints the extension listing that `curl --extensions-help` prints today, and `--extensions-help` is gone: curl answers it as any unknown option (exit 2).

## Context

Stewart, 2026-11-01: "rename the command line arg `--extensions-help` to `--extended-help`". BL-4452 (Done, `edd1261c9`) renamed `--extensions` to `--extensions-help` earlier the same day; follow what it changed.

On 2026-11-01 `extensions-help` / `ExtensionsHelp` appeared 100 times in 85 files: the option row and name list in `CommandLineOptionTable.cs`, `CommandLineOptions.ExtensionsHelpRequested` / `CommandLineGlobalState` (rename to `ExtendedHelpRequested`), `CurlCommandRunner` in `Curl.Console`, `CurlAiHelpText.cs` (its Curl-category row and index line), `McpOptionPathTable.cs` (the MCP `curl` tool's option list), `McpVersionToolResult.cs`, `ExtensionListing.cs`, `CurlCommandRunnerExtensionsTests.cs`, `LibcurlSourceCodeOptionCoverageTests.cs`, the golden `Curl.Mcp.IntegrationTests/Golden/resources-read.json`, ADR-1008, several CLAUDE.md files, and one doc comment in each extension's `Description` (about 60 `*Extension.cs` / `*Options.cs` files). Those doc-comment lines are one-word edits each; they are not listed in `touches` and need no other change. Find them with `rg -l "extensions-help|ExtensionsHelp"`.

Keep the names that are not the option: the `--ai-help extensions` subject and the MCP resource `curl://ai-help/extensions`. Check `ExtensionCommandLineOptions`' clash rule (no curl option or extension option is named `extended-help`). `--help` still leaves it out (ADR-1008).

## Acceptance criteria

- [ ] `curl --extended-help` prints exactly what `curl --extensions-help` printed before (and the off line under `CURL_EXTENSIONS=0`); `curl --extensions-help` now fails as an unknown option with exit 2; unit tests pin both.
- [ ] `rg "extensions-help|ExtensionsHelp"` finds nothing outside history (ADR amendment and log lines, Done tasks).
- [ ] `--ai-help` names `--extended-help`, and the MCP `curl` tool accepts `--extended-help` (`McpOptionPathTable`); the golden MCP files are re-recorded if they change.
- [ ] ADR-1008 carries an amendment for this rename.
- [ ] `dotnet build` and the fast tests pass; the touched libraries stay at 100% line and branch coverage.

## Notes

## Log

- 2026-11-01: Created.
- 2026-11-01: Backlog -> Doing. Criteria at claim: 1683e6c0 9700a053 65ac83db bcd3f8ca 91fd3925.
