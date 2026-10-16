---
id: BL-2387
title: Serve the --ai-help reference as MCP resources, one per category
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2321, BL-2376]
touches: [Curl.Mcp, Curl.Mcp.UnitLibrary, Curl.Mcp.UnitTests, Curl.Cli.UnitLibrary/Curl.Cli.UnitLibrary.csproj]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---

## Goal

Expose curl's --ai-help Markdown as MCP resources: an index and one resource per category.

## Context

ADR-0548 (`Documentation/Planning/Decisions/ADR-0548-curl-mcp-an-mcp-server-over-the-api-seam-on-microsofts-sdk.md`) and the plan `Documentation/Planning/Curl-Mcp.md` decide the design: `Curl.Mcp` is the thin host holding the Microsoft MCP SDK wiring, `Curl.Mcp.UnitLibrary` holds all tool logic (base class library only), `Curl.Mcp.UnitTests` tests both. The two packages `ModelContextProtocol` 2.2.0 and `Microsoft.Extensions.Hosting` 10.0.x are approved for `Curl.Mcp` only.

The text lives in `Curl.Cli.UnitLibrary/CurlAiHelpText.cs` (`TryGetMarkdown(subject, out markdown)`: the index for an empty subject, `all`, or a category name; categories come from `CurlHelpText.Categories`). Resource URIs: `curl://ai-help` (index), `curl://ai-help/all` and `curl://ai-help/<category>`, MIME `text/markdown`. The resource list is generated from the category table, never hand-written, so a new category appears automatically. The UnitLibrary holds a resource catalogue (uri, name, description, reader); `Curl.Mcp` holds only the SDK registration, as SDK 2.2.0 offers it.

## Acceptance criteria

- [x] A test asserts the catalogue contains the index, `all` and one entry per category in `CurlHelpText.Categories` and that each entry's text equals `CurlAiHelpText.TryGetMarkdown` for that subject.
- [x] An unknown `curl://ai-help/nope` yields a resource-not-found error, not an empty resource.
- [x] The server lists the resources and reads one through the service graph in a unit test.
- [x] `dotnet build Curl.Mcp -warnaserror` and `dotnet build Curl.Mcp.UnitLibrary -warnaserror` are clean; `dotnet test Curl.Mcp.UnitTests --filter "TestCategory!=Integration"` is green; `powershell -NoProfile -File Measure-CodeQuality.ps1` reports no member of `Curl.Mcp.UnitLibrary` or `Curl.Mcp` outside the four gates (100% line, 100% branch, complexity at most 10, CRAP at most 30).

## Notes

Plan: Documentation/Planning/Curl-Mcp.md. Decision: ADR-0548.

- Done (2026-10-15, lane 6): `McpAiHelpResource` and `McpAiHelpResourceCatalogue` in
  `Curl.Mcp.UnitLibrary`; `CurlAiHelpResourceHandlers` (list and read) registered in `CurlMcpHost`
  with `WithListResourcesHandler`/`WithReadResourceHandler`. Decisions in ADR-0576.
- Touches widened to `Curl.Cli.UnitLibrary/Curl.Cli.UnitLibrary.csproj` (no task in Doing on
  `origin/work/dark-factory` names it): `CurlHelpText.Categories` is internal, so the csproj grants
  `InternalsVisibleTo` to `Curl.Mcp.UnitLibrary` and `Curl.Mcp.UnitTests` rather than parsing printed
  help or widening Curl.Cli's public surface. No Curl.Cli code changed.
- Defaults taken: URIs match exactly (ordinal), so `curl://ai-help/HTTP` is unknown; an unknown URI is
  `McpProtocolException` with `ResourceNotFound` (-32002), as the criterion names it.
- Measured: Measure-CodeQuality -Library Curl.Mcp.UnitLibrary,Curl.Mcp: both 100% line, 100% branch,
  0 failing members. Curl.Mcp.UnitTests 356 passed; whole fast run green.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. Curl.Mcp serves --ai-help as MCP resources: curl://ai-help, curl://ai-help/all and one per category, unknown URIs are ResourceNotFound
