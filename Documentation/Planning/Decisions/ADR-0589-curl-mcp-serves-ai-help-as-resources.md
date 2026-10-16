# ADR-0589: `Curl.Mcp` serves the `--ai-help` reference as MCP resources

- Status: Accepted
- Date: 2026-10-15
- Decided by Claude under Stewart's delegation (BL-2387)

## Context

ADR-0548 and `Documentation/Planning/Curl-Mcp.md` give the MCP server the `--ai-help` Markdown as
resources: `curl://ai-help` (the index), `curl://ai-help/all` and `curl://ai-help/<category>`, MIME
`text/markdown`, listed from the category table so a new category appears without an edit. The
categories live in `CurlHelpText.Categories`, which is internal to `Curl.Cli.UnitLibrary`. The SDK
(ModelContextProtocol 2.2.0) offers resources either as `McpServerResource` objects, mostly built by
reflection over methods, or as plain `resources/list` and `resources/read` handlers. An unknown URI
must be an error, not an empty resource.

## Decision

- `Curl.Mcp.UnitLibrary` holds the catalogue: `McpAiHelpResourceCatalogue.Resources`, the index, then
  `all`, then one `McpAiHelpResource` (URI, name, description, subject) per category in
  `--help category` order, and `Find(uri)`. `McpAiHelpResource.ReadMarkdown` returns exactly what
  `CurlAiHelpText.TryGetMarkdown` gives for its subject.
- `Curl.Cli.UnitLibrary` grants `InternalsVisibleTo` to `Curl.Mcp.UnitLibrary` and `Curl.Mcp.UnitTests`,
  so the catalogue reads `CurlHelpText.Categories` itself rather than parsing printed help text or
  widening that library's public surface.
- `Curl.Mcp` registers `CurlAiHelpResourceHandlers.ListAsync` and `ReadAsync` with
  `WithListResourcesHandler` and `WithReadResourceHandler`: plain delegates, no reflection, so the host
  stays ahead-of-time compatible. The list fits one page; the cursor is ignored.
- URIs match exactly (ordinal): `curl://ai-help/HTTP` and `curl://ai-help/` are unknown, because a
  client reads the URIs it was listed, and a resource has one URI.
- An unknown URI throws `McpProtocolException` with `McpErrorCode.ResourceNotFound` (-32002), the code
  the MCP specification names for it and the one the SDK itself sends to clients of the protocol
  versions that predate the change to `InvalidParams`; either way the client sees an error.

## Consequences

A category added to `CurlHelpText.Categories` becomes a resource on the next build. The resources are
static text, so no `resources/templates/list` and no subscriptions are offered.
