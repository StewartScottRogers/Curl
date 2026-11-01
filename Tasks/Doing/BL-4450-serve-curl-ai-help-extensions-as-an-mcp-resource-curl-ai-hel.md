---
id: BL-4450
title: Serve curl --ai-help extensions as an MCP resource, curl://ai-help/extensions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Mcp.UnitLibrary/McpAiHelpResourceCatalogue.cs, Curl.Mcp.UnitLibrary/McpAiHelpResource.cs, Curl.Mcp/Tools/AiHelpMcpResourceProvider.cs]
requirement: none
created: 2026-11-01
completed:
---
# BL-4450 — Serve curl --ai-help extensions as an MCP resource, curl://ai-help/extensions

## Goal

An MCP client of `Curl.Mcp` can read Curl's extensions documentation as the resource `curl://ai-help/extensions`, the same Markdown `curl --ai-help extensions` prints, including "extensions are off" under `CURL_EXTENSIONS=0`.

## Context

Found 2026-11-01 checking how the hosts expose the extensions. `Curl.Console` answers `--ai-help extensions` with `Curl.Extension.Composition`'s `ExtensionListing.Markdown(readEnvironmentVariable)` (`CurlCommandRunner.WriteAiHelpAsync`, ADR-1008), and the `--ai-help` index (`CurlAiHelpText`, line ~159) tells the reader to run `curl --ai-help extensions`. `Curl.Mcp`'s resources (`McpAiHelpResourceCatalogue`, ADR-0589) are the index, `all` and one per `CurlHelpText.Categories` entry, each read through `CurlAiHelpText.TryGetMarkdown`, which has no `extensions` subject - so the MCP index resource points at a page the MCP server does not serve. The host already reads `CURL_EXTENSIONS` (`CurlMcpHost.CreateRegistry`, ADR-1071); `Curl.Mcp.UnitLibrary` should get the Markdown through an injected delegate rather than reference `Curl.Extension.Composition` itself if that reference is not already allowed (check `VerifyExtensionIsolation` in `Directory.Build.props`: only hosts and extensions may reference Composition).

`--help` stays as it is: it leaves the extensions out on purpose so curl's help pages stay byte for byte (ADR-1008).

## Acceptance criteria

- [ ] `resources/list` from `Curl.Mcp` includes `curl://ai-help/extensions`, after the category resources, with a description naming `curl --ai-help extensions`.
- [ ] Reading it returns exactly `ExtensionListing.Markdown` for the host's environment: the extensions' Markdown normally, and the off text when `CURL_EXTENSIONS` is `0`; unit tests in `Curl.Mcp.UnitTests` pin both.
- [ ] `Curl.Mcp/CLAUDE.md` names the new resource.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass, and `Curl.Mcp` and `Curl.Mcp.UnitLibrary` stay at 100% line and branch coverage.

## Notes

## Log

- 2026-11-01: Created.
- 2026-11-01: Backlog -> Doing. Criteria at claim: b631938b d0a97b81 16730235 d1aada98.
