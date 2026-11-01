---
id: BL-4451
title: Curl.Mcp serves the extended protocols as it serves curl's own: parse_url, build_url, version and transfer
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Mcp/CurlMcpHost.cs, Curl.Mcp/Tools/VersionMcpTool.cs, Curl.Mcp/Tools/ParseUrlMcpTool.cs, Curl.Mcp/Tools/BuildUrlMcpTool.cs, Curl.Mcp.UnitLibrary/McpUrlTool.cs, Curl.Mcp.UnitLibrary/McpVersionTool.cs, Curl.Mcp.UnitLibrary/McpVersionToolResult.cs, Curl.Mcp/Tools/TransferMcpTool.cs, Curl.Mcp/CLAUDE.md, Curl.Mcp.UnitLibrary/CLAUDE.md, Curl.Extension.Composition.UnitLibrary/ExtensionListing.cs, Curl.Extension.Composition.UnitLibrary/CLAUDE.md, Curl.Extension.Composition.UnitTests/ExtensionListingTests.cs, Curl.Mcp.UnitTests/CurlMcpHostExtensionTests.cs, Curl.Mcp.UnitTests/McpUrlToolTests.cs, Curl.Mcp.UnitTests/McpVersionToolTests.cs, Curl.Mcp.UnitTests/Tools/VersionMcpToolTests.cs]
requirement: none
created: 2026-11-01
completed: 2026-11-01
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

- [x] `parse_url` and `build_url` accept every extended scheme with no flag, as they accept `https`, and refuse them with `UnsupportedScheme` under `CURL_EXTENSIONS=0`; unit tests pin one extended and one original scheme each way.
- [x] The `version` tool's result lists every extended scheme, in `ExtensionDescriptionCatalogue` order, in its own field (empty under `CURL_EXTENSIONS=0`), and `protocols` is unchanged; the golden tools-list / output schema is updated.
- [x] A unit test runs the `transfer` tool on an extended-scheme URL through a stand-in extension handler and gets that handler's body back, as the same test does for an original scheme; the tool descriptions say extended schemes are accepted.
- [x] `Curl.Mcp/CLAUDE.md` and `Curl.Mcp.UnitLibrary/CLAUDE.md` describe the parity; an ADR records the design.
- [x] `dotnet build` and the fast tests pass; `Curl.Mcp` and `Curl.Mcp.UnitLibrary` stay at 100% line and branch coverage.

## Notes

- Design (ADR-1181): `ExtensionListing.Schemes(readEnvironmentVariable)` (public, in Composition) gives the catalogue's schemes in order, each once, none under `CURL_EXTENSIONS=0`; `CurlMcpHost.CreateRegistry` injects it into `McpUrlTool` (which adds `NonSupportScheme` only when the URL's or `scheme` part's scheme is one of them) and `McpVersionTool` (new `extendedProtocols` field after `protocols`, which stays curl's).
- Touches widened to `TransferMcpTool.cs` (description), `ExtensionListing.cs` and its test, the CLAUDE.md files and the test files; no task in Doing on origin/work/dark-factory named any of them.
- The golden tools-list pins description prefixes and suffixes and the input schemas, all unchanged; the new description sentences sit in the middle and are pinned by Contains, and the version result's new field is pinned in `VersionMcpToolTests` and `CurlMcpHostExtensionTests`.
- The transfer criterion's original-scheme counterpart is `TransferMcpToolTests.CallAsync_EveryField_...` (https body); the new test runs the registry's real engine through a stand-in extension handler and reads its body back.
- Measure-CodeQuality (-Library Curl.Mcp,Curl.Mcp.UnitLibrary,Curl.Extension.Composition.UnitLibrary): 100% line and branch for all three, 0 failing members. Full build clean; fast tests green (114 test assemblies). CI on Linux and macOS: the change is platform-neutral (no paths, OS text or certificates).

## Log

- 2026-11-01: Created.
- 2026-11-01: Backlog -> Doing. Criteria at claim: f0953fa5 857c3931 edc51a46 729e7cc8 e93fb10a.
- 2026-11-01: Doing -> Done. parse_url, build_url, version and transfer serve the extended protocols as curl's own, off under CURL_EXTENSIONS=0
