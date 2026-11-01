# ADR-1180: The MCP server serves `curl://ai-help/extensions` through a reader the host supplies

- Status: Accepted
- Date: 2026-11-01
- Amends ADR-0589
- Decided by Claude under Stewart's delegation (BL-4450)

## Context

`curl --ai-help` points the reader at `curl --ai-help extensions`, which `Curl.Console` answers with
`ExtensionListing.Markdown` (ADR-1008), but the MCP server's `--ai-help` resources (ADR-0589) were only the
subjects `CurlAiHelpText` answers, so the MCP index pointed at a page the server did not serve.
`ExtensionListing` lives in `Curl.Extension.Composition.UnitLibrary`, which only hosts and extensions may
reference (ADR-0999), so `Curl.Mcp.UnitLibrary`, where the resource catalogue lives, cannot call it.

## Decision

- `McpAiHelpResource` carries its reader as a `Func<string>`; the catalogue's `Resources` read
  `CurlAiHelpText` as before, and `McpAiHelpResourceCatalogue.ResourcesWithExtensions(readExtensionsMarkdown)`
  appends `curl://ai-help/extensions` (name `ai-help-extensions`) after the category resources.
- `AiHelpMcpResourceProvider` takes that reader; `CurlMcpHost.CreateRegistry` passes
  `() => ExtensionListing.Markdown(readEnvironmentVariable)`, the same environment reader that decides whether
  the engine reaches the extensions (ADR-1071). The resource therefore reads exactly what
  `curl --ai-help extensions` prints, including the "extensions are off" text under `CURL_EXTENSIONS=0`.
- The Markdown is read on each `resources/read`, not cached, as the other resources are.

## Consequences

- The MCP index's pointer to the extensions page resolves.
- `resources/list` gains one entry; the golden `resources-list.json` records it.
- `--help` is unchanged (ADR-1008).
