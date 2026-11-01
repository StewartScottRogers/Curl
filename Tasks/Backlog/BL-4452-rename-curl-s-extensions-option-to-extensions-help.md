---
id: BL-4452
title: Rename Curl's --extensions option to --extensions-help
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Cli.UnitLibrary/CommandLineOptions.cs, Curl.Cli.UnitLibrary/CommandLineGlobalState.cs, Curl.Cli.UnitLibrary/CurlAiHelpText.cs, Curl.Mcp.UnitLibrary/McpOptionPathTable.cs, Curl.Extension.Composition.UnitLibrary/ExtensionListing.cs]
requirement: none
created: 2026-11-01
completed:
---
# BL-4452 — Rename Curl's --extensions option to --extensions-help

## Goal

`curl --extensions-help` prints the extension listing that `curl --extensions` prints today, and `--extensions` is gone: curl answers it as any unknown option (exit 2).

## Context

Stewart, 2026-11-01: "In curl.Console change --extensions to --extensions-help."

The option is Curl's own (ADR-1008): `CommandLineOptionTable.cs` line ~327 (`CommandLineOption.Flag("extensions", ...)`) and the name list at line ~473, `CommandLineOptions.ExtensionsRequested` / `CommandLineGlobalState.ExtensionsRequested` (rename to `ExtensionsHelpRequested`), `CurlCommandRunner.ExtensionsOrBuildInformationLines` in `Curl.Console`, the `--ai-help` row in `CurlAiHelpText.cs` line ~53 and its index line ~159, `McpOptionPathTable.cs` line ~58 (the MCP curl tool's option list), `ExtensionListing`'s text, and every test, document, CLAUDE.md, wiki and ADR that names `--extensions`. The `--ai-help extensions` subject and the `curl://ai-help/extensions` MCP resource keep their names: they are a help subject, not the option. Check `ExtensionCommandLineOptions`' clash rule still passes (no curl option is named `extensions-help`). `--help` still leaves it out (ADR-1008). Amend ADR-1008 with the rename.

## Acceptance criteria

- [ ] `curl --extensions-help` prints exactly what `curl --extensions` printed before (and the off line under `CURL_EXTENSIONS=0`); `curl --extensions` now fails as an unknown option with exit 2; unit tests pin both.
- [ ] `--ai-help` (index and its Curl category) names `--extensions-help`, and no `.cs`, `.md` or script outside history (ADR log lines, Done tasks) still names the `--extensions` option.
- [ ] The MCP `curl` tool accepts `--extensions-help` and no longer lists `--extensions` (`McpOptionPathTable`).
- [ ] ADR-1008 carries an amendment for the rename.
- [ ] `dotnet build` and the fast tests pass; the touched libraries stay at 100% line and branch coverage.

## Notes

## Log

- 2026-11-01: Created.
