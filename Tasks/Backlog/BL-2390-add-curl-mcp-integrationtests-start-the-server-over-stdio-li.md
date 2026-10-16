---
id: BL-2390
title: Add Curl.Mcp.IntegrationTests: start the server over stdio, list tools, call curl against a loopback server, pack
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2321, BL-2384, BL-2385, BL-2386, BL-2387, BL-2388]
touches: [Curl.Mcp.IntegrationTests, Curl.slnx]
requirement: none
created: 2026-10-15
completed:
---

## Goal

Create the Integration test project that drives the real Curl.Mcp process over stdio.

## Context

ADR-0548 (`Documentation/Planning/Decisions/ADR-0548-curl-mcp-an-mcp-server-over-the-api-seam-on-microsofts-sdk.md`) and the plan `Documentation/Planning/Curl-Mcp.md` decide the design: `Curl.Mcp` is the thin host holding the Microsoft MCP SDK wiring, `Curl.Mcp.UnitLibrary` holds all tool logic (base class library only), `Curl.Mcp.UnitTests` tests both. The two packages `ModelContextProtocol` 2.2.0 and `Microsoft.Extensions.Hosting` 10.0.x are approved for `Curl.Mcp` only.

ADR-0421: an Integration test touches something real outside the process (a child process, a socket, the disk). The project sits directly before `Curl.Mcp` in the alphabetical run (the naming rule in root `CLAUDE.md`), every test carries `[TestCategory("Integration")]`, and `Directory.Build.props`' `VerifyIntegrationTestPlacement` enforces it. ADR-0548 confines the SDK packages to `Curl.Mcp`, so write the small JSON-RPC stdio client by hand with `System.Text.Json`. For the loopback server reuse the helpers other IntegrationTests projects use (see `Curl.Console.IntegrationTests`).

## Acceptance criteria

- [ ] Project `Curl.Mcp.IntegrationTests` exists, is in `Curl.slnx` directly before `Curl.Mcp`, and every test carries `TestCategory(Integration)`; the fast test command skips them all.
- [ ] Tests start the built `Curl.Mcp` as a child process with `CURL_MCP_ROOT` set to a temp directory, send `initialize` and `tools/list` and assert the five tools and the resources; call `version`; call `curl` with `args` `["-sS", "http://127.0.0.1:<port>/"]` against a loopback listener and assert stdout, exit 0 and the request the listener saw.
- [ ] A confinement test: `curl` with `-o` and an absolute path outside the root (platform-appropriate) returns a tool error naming `-o` and writes nothing; `-o inside.txt` writes inside the root.
- [ ] A pack test runs `dotnet pack Curl.Mcp` into a temp folder and asserts the `.nupkg` contains `.mcp/server.json` and its nuspec has the McpServer package type.
- [ ] `dotnet test Curl.Mcp.IntegrationTests --filter "TestCategory=Integration"` is green on Windows; `dotnet test --filter "TestCategory!=Integration"` is unaffected; `dotnet build -warnaserror` is clean.

## Notes

Plan: Documentation/Planning/Curl-Mcp.md. Decision: ADR-0548.

BL-2384 (2026-10-16) already created `Curl.Mcp.IntegrationTests`, in `Curl.slnx` directly before
`Curl.Mcp`, with `CurlTransferToolIntegrationTests` (the `transfer` tool against a loopback
listener through the host's composition); this task adds its tests to that project.

## Log

- 2026-10-15: Created.
