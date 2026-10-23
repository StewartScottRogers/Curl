---
id: BL-3381
title: Hand-write the MCP resources/list and resources/read method handlers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3378, BL-3379]
touches: [Curl.Mcp.Protocol.UnitLibrary/Resources, Curl.Mcp.Protocol.UnitTests/Resources]
requirement: none
created: 2026-10-23
completed:
---
# BL-3381 — Hand-write the MCP resources/list and resources/read method handlers

## Goal

`Curl.Mcp.Protocol.UnitLibrary/Resources/` holds the `resources/list` and `resources/read` method handlers over the registered `IMcpResourceProvider`s, with the same answers as the SDK server.

## Context

- MCP transport and message rules: https://modelcontextprotocol.io/specification (read the protocol revision the golden conversations recorded by BL-3374 show the SDK 2.2.0 server negotiating, and say which revision you read). Handlers implement `IMcpMethodHandler` from BL-3379; providers come from BL-3378. Each handler is its own file and contributes the `resources` capability (the same flags the SDK server's `initialize` answer carries in the golden files of BL-3374).
- `resources/list` returns every resource with `uri`, `name`, `description`, `mimeType`; a `cursor` is accepted and ignored. `resources/read` returns `contents` with one text content; an unknown URI, or `params` with no URI, is a JSON-RPC error with code -32002 (resource not found) and the message `Resource not found: '<uri>'.`, as `CurlAiHelpResourceHandlers` does today.
- Tests use fake providers defined in the test project, one file each.

## Acceptance criteria

- [ ] `ResourcesListHandler` and `ResourcesReadHandler` exist under `Resources/`, one type per file, with XML doc comments.
- [ ] Tests cover listing, reading a known URI, an unknown URI (-32002 and the message above), missing params, several providers, and a cursor being ignored.
- [ ] Where BL-3374's golden files exist, a test asserts equal JSON values for the golden `resources/list` and missing-URI responses with equivalent fake providers; deliberate differences are listed in Notes.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Mcp.Protocol.UnitLibrary` reports 0 failing members (100% line, 100% branch, complexity at most 10, CRAP at most 30), measured from the fast tests only.
- [ ] `dotnet build` of every project named in touches is clean with `-warnaserror`, and `dotnet test --filter "TestCategory!=Integration"` passes for the test projects named in touches, with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: fefc5816 e551d4c4 6fe1cfe1 bf223748 6d2cec38.
