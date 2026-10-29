---
id: BL-4159
title: Extension: Call Model Context Protocol servers with mcp://
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4092, BL-4214, BL-4215, BL-4216]
touches: [Curl.Extension.McpClient.UnitLibrary, Curl.Extension.McpClient.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed:
---
# BL-4159 — Extension: Call Model Context Protocol servers with mcp://

## Goal

Curl provides `mcp://` (Streamable HTTP) URLs and a local-process form: list and call tools, as the extension `Curl.Extension.McpClient.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 59 of 63. Specification: Model Context Protocol specification (latest dated revision). It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [ ] `Curl.Extension.McpClient.UnitLibrary` and `Curl.Extension.McpClient.UnitTests` exist in `/Extensions/McpClient/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [ ] It provides `mcp://` (Streamable HTTP) URLs and a local-process form: list and call tools.
- [ ] Unit tests check its wire behaviour against named sections of Model Context Protocol specification (latest dated revision), with no network (fake connections or recorded bytes).
- [ ] With `CURL_EXTENSIONS=0` a URL with its scheme fails exactly as curl fails an unsupported scheme (exit 1, same stderr bytes); unit test.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [ ] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29 (lane 7): Split for size before any code, as the Notes above ask. BL-4214 creates both projects, the slnx line and the JSON-RPC wire format; BL-4215 the Streamable HTTP transport (with its own minimal HTTP/1.1 client, since an extension may not reference Curl.Protocol.Http); BL-4216 the stdio local-process transport. What is left here: the `IProtocolHandler` (URL forms: suggested `mcp://` over HTTP and `mcps://` over TLS, and a local-process form, decided in the ADR), the catalogue and description lines, the `CURL_EXTENSIONS=0` unsupported-scheme test, the ADR and the quality run. Hooks checked present: `ProtocolHandlerServices`, `ExtensionCatalogue`, `ExtensionDescriptionCatalogue`, `ExtensionProtocolHandlers.For`.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 869c1d51 f27cae12 bc209cf3 bc772cb6 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Backlog. Split for size before any code: waits on BL-4214 (project and wire format), BL-4215 (Streamable HTTP transport) and BL-4216 (stdio transport)
