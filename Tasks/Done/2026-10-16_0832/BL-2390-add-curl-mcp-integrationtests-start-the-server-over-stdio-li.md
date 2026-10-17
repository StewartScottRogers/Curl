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
completed: 2026-10-16
---

## Goal

Create the Integration test project that drives the real Curl.Mcp process over stdio.

## Context

ADR-0548 (`Documentation/Planning/Decisions/ADR-0548-curl-mcp-an-mcp-server-over-the-api-seam-on-microsofts-sdk.md`) and the plan `Documentation/Planning/Curl-Mcp.md` decide the design: `Curl.Mcp` is the thin host holding the Microsoft MCP SDK wiring, `Curl.Mcp.UnitLibrary` holds all tool logic (base class library only), `Curl.Mcp.UnitTests` tests both. The two packages `ModelContextProtocol` 2.2.0 and `Microsoft.Extensions.Hosting` 10.0.x are approved for `Curl.Mcp` only.

ADR-0421: an Integration test touches something real outside the process (a child process, a socket, the disk). The project sits directly before `Curl.Mcp` in the alphabetical run (the naming rule in root `CLAUDE.md`), every test carries `[TestCategory("Integration")]`, and `Directory.Build.props`' `VerifyIntegrationTestPlacement` enforces it. ADR-0548 confines the SDK packages to `Curl.Mcp`, so write the small JSON-RPC stdio client by hand with `System.Text.Json`. For the loopback server reuse the helpers other IntegrationTests projects use (see `Curl.Console.IntegrationTests`).

## Acceptance criteria

- [x] Project `Curl.Mcp.IntegrationTests` exists, is in `Curl.slnx` directly before `Curl.Mcp`, and every test carries `TestCategory(Integration)`; the fast test command skips them all.
- [x] Tests start the built `Curl.Mcp` as a child process with `CURL_MCP_ROOT` set to a temp directory, send `initialize` and `tools/list` and assert the five tools and the resources; call `version`; call `curl` with `args` `["-sS", "http://127.0.0.1:<port>/"]` against a loopback listener and assert stdout, exit 0 and the request the listener saw.
- [x] A confinement test: `curl` with `-o` and an absolute path outside the root (platform-appropriate) returns a tool error naming `-o` and writes nothing; `-o inside.txt` writes inside the root.
- [x] A pack test runs `dotnet pack Curl.Mcp` into a temp folder and asserts the `.nupkg` contains `.mcp/server.json` and its nuspec has the McpServer package type.
- [x] `dotnet test Curl.Mcp.IntegrationTests --filter "TestCategory=Integration"` is green on Windows; `dotnet test --filter "TestCategory!=Integration"` is unaffected; `dotnet build -warnaserror` is clean.

## Notes

Plan: Documentation/Planning/Curl-Mcp.md. Decision: ADR-0548.

BL-2384 (2026-10-16) already created `Curl.Mcp.IntegrationTests`, in `Curl.slnx` directly before
`Curl.Mcp`, with `CurlTransferToolIntegrationTests` (the `transfer` tool against a loopback
listener through the host's composition); this task adds its tests to that project.

BL-2390 (2026-10-16): added `McpStdioClient` (a hand-written newline-delimited JSON-RPC client over
the child's stdio), `LoopbackHttpResponder` (the one-response helper, moved out of
`CurlTransferToolIntegrationTests` so both classes share it), `CurlMcpServerIntegrationTests`
(7 tests) and `CurlMcpPackIntegrationTests` (1 test). Choices:
- The server starts as `dotnet exec --runtimeconfig <this project's> --depsfile <this project's>
  Curl.Mcp.dll` from the test output: the referenced exe's own runtimeconfig is not guaranteed to be
  copied there, and the test's deps file already lists every assembly Curl.Mcp needs. The host is
  `DOTNET_HOST_PATH`, else `dotnet`.
- The outside path is a fresh folder beside the root under the temp folder, absolute on every platform.
- Pack runs `-c Release`, so it never rewrites the Debug output the other tests run from; about 25 s cold.
- `Curl.slnx` needed no change (BL-2384 had added the project). No production code changed, so no
  quality run was needed.

Measured: `dotnet test Curl.Mcp.IntegrationTests --filter "TestCategory=Integration"` 9/9 passed on
Windows; `dotnet build -warnaserror` clean, 0 warnings; fast tests all green.

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. Curl.Mcp.IntegrationTests drives the real server over stdio (initialize, tools/list, resources/list, version, curl loopback and -o confinement) and checks dotnet pack's McpServer package
