# ADR-1181: The MCP tools serve Curl's extended protocols as they serve curl's own

- Status: Accepted
- Date: 2026-11-01
- Amends ADR-0548
- Decided by Claude under Stewart's delegation (BL-4451)

## Context

Stewart asked that the extended protocols (`Curl.Extended.Protocol.*`, ADR-1177) be served by the `Curl.Mcp`
host exactly as curl's own protocols are. The `curl` tool and the `transfer` tool's engine already reached the
extensions' handlers (ADR-1071), but `parse_url` and `build_url` refused an extended scheme as
`CURLUE_UNSUPPORTED_SCHEME` unless the caller passed `NonSupportScheme`, and the `version` tool listed only curl's
protocols, so an agent could not discover the extended schemes. `Libcurl`, `curl -V` and
`CurlVersionInfo.Protocols` must stay curl's (ADR-0999), and `Curl.Mcp.UnitLibrary` may not reference
`Curl.Extension.Composition.UnitLibrary`.

## Decision

- `ExtensionListing.Schemes(readEnvironmentVariable)` gives every registered extension's schemes, in
  `ExtensionDescriptionCatalogue` order, each once, or none when `CURL_EXTENSIONS` is `0`.
- `CurlMcpHost.CreateRegistry` reads it once and injects it into `McpUrlTool` and `McpVersionTool`.
- `McpUrlTool` adds `CurlUrlFlags.NonSupportScheme` to the set of a URL (`parse_url`) or of every part
  (`build_url`) whose scheme is one of those schemes, matched without regard to case. Curl's URL parser itself
  is unchanged, so `Libcurl`'s `curl_url` still refuses them as libcurl does.
- The `version` tool's result gains `extendedProtocols`, after `protocols`; `protocols` stays the engine's
  `CurlVersionInfo.Protocols`, curl's list.
- The `transfer`, `parse_url`, `build_url` and `version` descriptions say extended schemes are accepted.

## Consequences

- An agent finds the extended schemes in `version` and parses, builds and transfers their URLs with no flag.
- Under `CURL_EXTENSIONS=0` every tool behaves as before: extended schemes are unsupported and
  `extendedProtocols` is empty.
- Adding the flag only for a listed scheme keeps every other scheme's answer exactly libcurl's.
