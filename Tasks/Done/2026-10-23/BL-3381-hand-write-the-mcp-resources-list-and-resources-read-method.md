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
completed: 2026-10-23
---
# BL-3381 — Hand-write the MCP resources/list and resources/read method handlers

## Goal

`Curl.Mcp.Protocol.UnitLibrary/Resources/` holds the `resources/list` and `resources/read` method handlers over the registered `IMcpResourceProvider`s, with the same answers as the SDK server.

## Context

- MCP transport and message rules: https://modelcontextprotocol.io/specification (read the protocol revision the golden conversations recorded by BL-3374 show the SDK 2.2.0 server negotiating, and say which revision you read). Handlers implement `IMcpMethodHandler` from BL-3379; providers come from BL-3378. Each handler is its own file and contributes the `resources` capability (the same flags the SDK server's `initialize` answer carries in the golden files of BL-3374).
- `resources/list` returns every resource with `uri`, `name`, `description`, `mimeType`; a `cursor` is accepted and ignored. `resources/read` returns `contents` with one text content; an unknown URI, or `params` with no URI, is a JSON-RPC error with code -32002 (resource not found) and the message `Resource not found: '<uri>'.`, as `CurlAiHelpResourceHandlers` does today.
- Tests use fake providers defined in the test project, one file each.

## Acceptance criteria

- [x] `ResourcesListHandler` and `ResourcesReadHandler` exist under `Resources/`, one type per file, with XML doc comments.
- [x] Tests cover listing, reading a known URI, an unknown URI (-32002 and the message above), missing params, several providers, and a cursor being ignored.
- [x] Where BL-3374's golden files exist, a test asserts equal JSON values for the golden `resources/list` and missing-URI responses with equivalent fake providers; deliberate differences are listed in Notes.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Mcp.Protocol.UnitLibrary` reports 0 failing members (100% line, 100% branch, complexity at most 10, CRAP at most 30), measured from the fast tests only.
- [x] `dotnet build` of every project named in touches is clean with `-warnaserror`, and `dotnet test --filter "TestCategory!=Integration"` passes for the test projects named in touches, with no test deleted, skipped or weakened.

## Notes

- Read MCP revision 2025-06-18 (the revision the golden resources conversations negotiate); its resources sections are unchanged in substance in 2025-11-25.
- Decisions in ADR-0804: only `ResourcesListHandler` contributes the `resources: {}` capability (two contributors would write the member twice in `initialize`); the cursor is ignored; params with no string `uri` (none, `{}`, a number, an array) answer -32002 `Resource not found: ''.`; a listed URI its provider reads as null also answers -32002.
- Golden tests: `ResourcesListHandlerTests` builds a fake provider from the golden `resources/list` result (28 resources) and asserts `JsonElement.DeepEquals`; `ResourcesReadHandlerTests` asserts the golden missing-URI error's code and message. The golden values are embedded as constants, since the unit tests do not read Curl.Mcp.IntegrationTests' files. No deliberate differences from the golden answers.
- Measure-CodeQuality.ps1 -Library Curl.Mcp.Protocol.UnitLibrary: 0 failing members. dotnet build -warnaserror clean; Curl.Mcp.Protocol.UnitTests fast tests 134 passed.
- CI on Linux and macOS: the tests touch no path, file or OS text, so they are platform-neutral.
## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: fefc5816 e551d4c4 6fe1cfe1 bf223748 6d2cec38.
- 2026-10-23: Doing -> Done. resources/list and resources/read handlers answer as the SDK server's golden files
