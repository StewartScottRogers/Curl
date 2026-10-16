---
id: BL-2486
title: Publish Curl.Mcp as native AOT: get every tool's parameters marshalled on the AOT path despite ModelContextProtocol 2.2.0
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2485]
touches: [Curl.Mcp, Curl.Mcp.UnitTests, Curl.Mcp.IntegrationTests, Curl.Mcp.UnitLibrary, Curl.Mcp.Protocol.UnitLibrary, Curl.Mcp.Protocol.UnitTests, Curl.slnx, Directory.Packages.props, .github/workflows/release.yml]
requirement: none
created: 2026-10-16
completed:
---
# BL-2486 — Publish Curl.Mcp as native AOT: get every tool's parameters marshalled on the AOT path despite ModelContextProtocol 2.2.0

## Goal

`dotnet publish Curl.Mcp -r <rid> -p:PublishAot=true` gives a native binary that answers `initialize`, lists `curl`, `transfer`, `parse_url`, `build_url` and `version` in `tools/list`, and runs each one, and `dotnet pack Curl.Mcp` packs those native binaries per runtime identifier, as ADR-0548 decision 6 asks.

## Context

- ADR-0598 records the failure on `ModelContextProtocol` 2.2.0, SDK 10.0.402: the AOT build succeeds with no warning (30,222,336-byte `Curl.Mcp.exe`), but the host stops before `initialize` with `System.NotSupportedException: JsonTypeInfo metadata for type 'Curl.Mcp.McpTransferHeader[]' was not provided by TypeInfoResolver of type '[ModelContextProtocol.McpJsonUtilities+JsonContext, Microsoft.Extensions.AI.AIJsonUtilities+JsonContext]'`, thrown from `AIFunctionFactory.ReflectionAIFunctionDescriptor.GetParameterMarshaller` under `AIFunctionMcpServerTool.Create` for the `transfer` tool. `CurlMcpHost` passes `CurlMcpJsonContext.ToolOptions` to every `WithTools<T>`, but on the AOT path the marshaller sees only the SDK's own two resolvers.
- Stewart, 2026-10-16, chose not to wait for an SDK upgrade: work around it in Curl.Mcp. Ways to try, simplest first: give `transfer` parameters the SDK's resolvers already know (e.g. headers as `string[]` of `Name: Value` lines, or a `JsonElement`, parsed and checked in `Curl.Mcp.UnitLibrary` by `McpTransferRequestCheck`); register the tools through the `McpServerTool.Create` / `AIFunctionFactoryOptions` overload that takes `SerializerOptions`, so the source-generated context reaches parameter marshalling; or insert `CurlMcpJsonContext` into the resolver chain the SDK uses. Record which one worked, and why the others did not, in a new ADR that supersedes ADR-0598's decision.
- **Fallback (Stewart, 2026-10-16): if no workaround makes the SDK run under AOT** - the first fix only uncovers a second SDK failure, or the fix needs an SDK upgrade or a new package - do not block. Replace the SDK with a hand-written MCP stdio server instead: newline-delimited JSON-RPC 2.0 over standard input and output, with `initialize` (protocol version negotiation, server info, the `tools` capability), `notifications/initialized`, `ping`, `tools/list` and `tools/call`, plus error responses for unknown methods and bad parameters, as the MCP specification the SDK targets defines them. Put it in a new `Curl.Mcp.Protocol.UnitLibrary` with its own `Curl.Mcp.Protocol.UnitTests` (the `new-project` skill; Curl.slnx order kept), BCL only, `System.Text.Json` source-generated, held to the four quality gates. `Curl.Mcp` then drops its `ModelContextProtocol` and `Microsoft.Extensions.Hosting` references (removing a package needs no approval; remove their `Directory.Packages.props` entries once nothing references them) and keeps the same five tools with the same names, descriptions and input schemas. Update ADR-0548's SDK decision in the new ADR, and the root CLAUDE.md's package exception only through a note under Notes for an interactive session, since this task does not touch CLAUDE.md.
- A change to a tool's input schema is visible to agents: if `transfer`'s `headers` changes shape, `Curl.Mcp/README.md`, the tool's description and every test that sends headers change with it, and the schema stays one an agent can fill without guessing.
- No new package and no package upgrade without Stewart (CLAUDE.md). `Curl.Mcp` is the only project allowed `ModelContextProtocol` and `Microsoft.Extensions.Hosting`.
- `dotnet publish` with AOT needs `C:\Program Files (x86)\Microsoft Visual Studio\Installer` on PATH (vswhere). Native AOT cannot cross-compile between operating systems, so packing native per runtime means a pack per OS: `.github/workflows/release.yml` (after BL-2485 adds the Curl.Mcp pack there) runs it on each OS's runner; `ci.yml`'s `mcp-pack` job is a guard file and is left alone, so if it must change, file an interactive task for it.

## Acceptance criteria

- [ ] On Windows, `dotnet publish Curl.Mcp -r win-x64 -p:PublishAot=true` builds with no trim or AOT warning, and the binary over stdio answers `initialize`, lists the five tools in `tools/list`, and a `transfer` call with one request header against a loopback server returns the response; the commands and output are in Notes.
- [ ] `Curl.Mcp.IntegrationTests` has a test that publishes or runs the AOT binary and makes the same `initialize`, `tools/list` and `transfer` calls, marked `[OSCondition]` only where AOT tooling is missing.
- [ ] `PublishAot` is on for Curl.Mcp's publish and pack, and `dotnet pack Curl.Mcp` still gives the tool package plus one package per runtime identifier, each with a native binary for its runtime, built on its own OS in `release.yml`.
- [ ] A new ADR records the workaround and supersedes ADR-0598's decision; `Curl.Mcp/README.md`'s Package section and ADR-0548's packing note are true.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Mcp,Curl.Mcp.UnitLibrary` (plus `Curl.Mcp.Protocol.UnitLibrary` if the fallback was taken) reports 0 failing members.
- [ ] Notes say which path was taken (SDK workaround or hand-written server) and, for the fallback, what the workaround attempts hit.

## Notes

## Log

- 2026-10-16: Created.
