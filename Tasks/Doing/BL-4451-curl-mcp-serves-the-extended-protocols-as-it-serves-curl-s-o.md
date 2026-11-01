---
id: BL-4451
title: Curl.Mcp serves the extended protocols as it serves curl's own: parse_url, build_url, version and transfer
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Mcp/CurlMcpHost.cs, Curl.Mcp/Tools/VersionMcpTool.cs, Curl.Mcp/Tools/ParseUrlMcpTool.cs, Curl.Mcp/Tools/BuildUrlMcpTool.cs, Curl.Mcp.UnitLibrary/McpUrlTool.cs, Curl.Mcp.UnitLibrary/McpVersionTool.cs, Curl.Mcp.UnitLibrary/McpVersionToolResult.cs]
requirement: none
created: 2026-11-01
completed:
---
# BL-4451 — Curl.Mcp serves the extended protocols as it serves curl's own: parse_url, build_url, version and transfer

## Goal

Every `Curl.Mcp` tool treats a URL of an extended protocol (`redis://`, `kafka://`, `coap://`, `mqtt://`, `s3://` and the rest of `Curl.Extended.Protocol.*`) exactly as it treats a URL of one of curl's own protocols, unless `CURL_EXTENSIONS` is `0`.

## Context

Stewart, 2026-11-01: "Make sure that the Extended protocols are implemented the same as the original Protocols in the Curl.Mcp host."

Found that day: the `curl` tool (`InProcessCurlCommandRunner`) and the `transfer` tool's engine (`CurlMcpHost.CreateRegistry` passes `ExtensionProtocolHandlers` to `ManagedCurlEngine`, ADR-1071) already reach the extended handlers. The gaps are:

- `parse_url` / `build_url` (`McpUrlTool` over the engine's `CreateUrl`): the URL API knows only curl's schemes, so an extended scheme is refused as `CURLUE_UNSUPPORTED_SCHEME` unless the caller passes `NonSupportScheme`, where an original scheme needs no flag.
- `version` (`McpVersionTool`): `Protocols` is `ManagedCurlVersion`'s static list (curl -V's), so no extended scheme appears; an agent cannot discover them from the tool.
- `transfer`: no test pins that an extended-scheme URL runs through it; its descriptions and `McpTransferRequestCheck` were written for HTTP.

Rules that hold: `Libcurl`, `curl -V` and `CurlVersionInfo.Protocols` stay curl's (ADR-0999), so the extended schemes go in a field of their own (for example `extendedProtocols`) rather than into `protocols`. `Curl.Mcp.UnitLibrary` does not reference `Curl.Extension.Composition` (`VerifyExtensionIsolation`); the host injects the extended schemes (`ExtensionDescriptionCatalogue` descriptions' `Schemes`, through a public accessor on `Curl.Extension.Composition` if needed) and the URL tool's scheme acceptance. Record the design in an ADR (Decided by Claude under Stewart's delegation). Tool logic stays in `Curl.Mcp.UnitLibrary` (Curl.Mcp/CLAUDE.md).

## Acceptance criteria

- [ ] `parse_url` and `build_url` accept every extended scheme with no flag, as they accept `https`, and refuse them with `UnsupportedScheme` under `CURL_EXTENSIONS=0`; unit tests pin one extended and one original scheme each way.
- [ ] The `version` tool's result lists every extended scheme, in `ExtensionDescriptionCatalogue` order, in its own field (empty under `CURL_EXTENSIONS=0`), and `protocols` is unchanged; the golden tools-list / output schema is updated.
- [ ] A unit test runs the `transfer` tool on an extended-scheme URL through a stand-in extension handler and gets that handler's body back, as the same test does for an original scheme; the tool descriptions say extended schemes are accepted.
- [ ] `Curl.Mcp/CLAUDE.md` and `Curl.Mcp.UnitLibrary/CLAUDE.md` describe the parity; an ADR records the design.
- [ ] `dotnet build` and the fast tests pass; `Curl.Mcp` and `Curl.Mcp.UnitLibrary` stay at 100% line and branch coverage.

## Notes

## Log

- 2026-11-01: Created.
- 2026-11-01: Backlog -> Doing. Criteria at claim: f0953fa5 857c3931 edc51a46 729e7cc8 e93fb10a.
