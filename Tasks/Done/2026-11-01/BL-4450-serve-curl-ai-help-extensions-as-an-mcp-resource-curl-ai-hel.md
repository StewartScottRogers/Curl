---
id: BL-4450
title: Serve curl --ai-help extensions as an MCP resource, curl://ai-help/extensions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Mcp.UnitLibrary/McpAiHelpResourceCatalogue.cs, Curl.Mcp.UnitLibrary/McpAiHelpResource.cs, Curl.Mcp.UnitLibrary/CLAUDE.md, Curl.Mcp.UnitLibrary/README.md, Curl.Mcp/Tools/AiHelpMcpResourceProvider.cs, Curl.Mcp/CurlMcpHost.cs, Curl.Mcp/CLAUDE.md, Curl.Mcp.UnitTests/McpAiHelpResourceCatalogueTests.cs, Curl.Mcp.UnitTests/Tools/AiHelpMcpResourceProviderTests.cs, Curl.Mcp.UnitTests/CurlMcpHostTests.cs, Curl.Mcp.UnitTests/CurlMcpHostExtensionTests.cs, Curl.Mcp.IntegrationTests/CurlMcpServerIntegrationTests.cs, Curl.Mcp.IntegrationTests/Golden/resources-list.json]
requirement: none
created: 2026-11-01
completed: 2026-11-01
---
# BL-4450 — Serve curl --ai-help extensions as an MCP resource, curl://ai-help/extensions

## Goal

An MCP client of `Curl.Mcp` can read Curl's extensions documentation as the resource `curl://ai-help/extensions`, the same Markdown `curl --ai-help extensions` prints, including "extensions are off" under `CURL_EXTENSIONS=0`.

## Context

Found 2026-11-01 checking how the hosts expose the extensions. `Curl.Console` answers `--ai-help extensions` with `Curl.Extension.Composition`'s `ExtensionListing.Markdown(readEnvironmentVariable)` (`CurlCommandRunner.WriteAiHelpAsync`, ADR-1008), and the `--ai-help` index (`CurlAiHelpText`, line ~159) tells the reader to run `curl --ai-help extensions`. `Curl.Mcp`'s resources (`McpAiHelpResourceCatalogue`, ADR-0589) are the index, `all` and one per `CurlHelpText.Categories` entry, each read through `CurlAiHelpText.TryGetMarkdown`, which has no `extensions` subject - so the MCP index resource points at a page the MCP server does not serve. The host already reads `CURL_EXTENSIONS` (`CurlMcpHost.CreateRegistry`, ADR-1071); `Curl.Mcp.UnitLibrary` should get the Markdown through an injected delegate rather than reference `Curl.Extension.Composition` itself if that reference is not already allowed (check `VerifyExtensionIsolation` in `Directory.Build.props`: only hosts and extensions may reference Composition).

`--help` stays as it is: it leaves the extensions out on purpose so curl's help pages stay byte for byte (ADR-1008).

## Acceptance criteria

- [x] `resources/list` from `Curl.Mcp` includes `curl://ai-help/extensions`, after the category resources, with a description naming `curl --ai-help extensions`.
- [x] Reading it returns exactly `ExtensionListing.Markdown` for the host's environment: the extensions' Markdown normally, and the off text when `CURL_EXTENSIONS` is `0`; unit tests in `Curl.Mcp.UnitTests` pin both.
- [x] `Curl.Mcp/CLAUDE.md` names the new resource.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass, and `Curl.Mcp` and `Curl.Mcp.UnitLibrary` stay at 100% line and branch coverage.

## Notes

- Design (ADR-1180, decided by Claude under Stewart's delegation): `McpAiHelpResource` carries a `Func<string>` reader; `McpAiHelpResourceCatalogue.ResourcesWithExtensions` appends `curl://ai-help/extensions`, and `CurlMcpHost.CreateRegistry` passes `() => ExtensionListing.Markdown(readEnvironmentVariable)` to `AiHelpMcpResourceProvider`, so `Curl.Mcp.UnitLibrary` never references Composition.
- Touches widened to `CurlMcpHost.cs` (the wiring), both CLAUDE.md files, the UnitLibrary README, the Curl.Mcp tests and the integration golden `resources-list.json` plus the resource count in `CurlMcpServerIntegrationTests` (both would fail otherwise); no task in Doing on origin/work/dark-factory held any of them.
- Measured: Curl.Mcp.UnitTests 535 passed; Curl.Mcp.IntegrationTests golden and resources tests 31 passed; Measure-CodeQuality 0 failing members for Curl.Mcp.UnitLibrary and Curl.Mcp; full build clean, fast tests green. CI on Linux and macOS: the change is platform-neutral (no paths, OS text or certificates).

## Log

- 2026-11-01: Created.
- 2026-11-01: Backlog -> Doing. Criteria at claim: b631938b d0a97b81 16730235 d1aada98.
- 2026-11-01: Doing -> Done. curl://ai-help/extensions served by Curl.Mcp, reading ExtensionListing.Markdown in the host's environment
