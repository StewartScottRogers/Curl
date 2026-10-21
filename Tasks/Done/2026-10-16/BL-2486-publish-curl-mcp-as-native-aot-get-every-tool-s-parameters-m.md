---
id: BL-2486
title: Publish Curl.Mcp as native AOT: get every tool's parameters marshalled on the AOT path despite ModelContextProtocol 2.2.0
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2485]
touches: [Curl.Mcp, Curl.Mcp.UnitTests, Curl.Mcp.IntegrationTests, Curl.Mcp.UnitLibrary, Curl.Mcp.Protocol.UnitLibrary, Curl.Mcp.Protocol.UnitTests, Curl.slnx, Directory.Packages.props, .github/workflows/release.yml, Documentation/Planning/Decisions/ADR-0548-curl-mcp-an-mcp-server-over-the-api-seam-on-microsofts-sdk.md, Documentation/Planning/Decisions/ADR-0598-curl-mcp-native-aot-fails-keeps-self-contained.md]
requirement: none
created: 2026-10-16
completed: 2026-10-16
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

- [x] On Windows, `dotnet publish Curl.Mcp -r win-x64 -p:PublishAot=true` builds with no trim or AOT warning, and the binary over stdio answers `initialize`, lists the five tools in `tools/list`, and a `transfer` call with one request header against a loopback server returns the response; the commands and output are in Notes.
- [x] `Curl.Mcp.IntegrationTests` has a test that publishes or runs the AOT binary and makes the same `initialize`, `tools/list` and `transfer` calls, marked `[OSCondition]` only where AOT tooling is missing.
- [x] `PublishAot` is on for Curl.Mcp's publish and pack, and `dotnet pack Curl.Mcp` still gives the tool package plus one package per runtime identifier, each with a native binary for its runtime, built on its own OS in `release.yml`.
- [x] A new ADR records the workaround and supersedes ADR-0598's decision; `Curl.Mcp/README.md`'s Package section and ADR-0548's packing note are true.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Mcp,Curl.Mcp.UnitLibrary` (plus `Curl.Mcp.Protocol.UnitLibrary` if the fallback was taken) reports 0 failing members.
- [x] Notes say which path was taken (SDK workaround or hand-written server) and, for the fallback, what the workaround attempts hit.

## Notes

- **Path taken: an SDK-free fix in Curl.Mcp; no SDK workaround and no hand-written server were needed.** ADR-0621 supersedes ADR-0598.
- Root cause: `CurlMcpJsonContext.ToolOptions` was a static auto-property initializer passing the generator's `Default` to `JsonTypeInfoResolver.Combine`. The generator sets `Default` in its own partial file's static initializer, which ran later, so `Combine` got `null` and dropped it silently. Under the JIT the reflection fallback hid it. Fix: `ToolOptions => field ??= ...` (built on first use).
- Reproduced in a minute without AOT: `dotnet build Curl.Mcp -c Release -p:JsonSerializerIsReflectionEnabledByDefault=false -o <dir>`, then stdio. The workaround attempts hit the same error, which is what exposed the cause: (1) every tool through `McpServerTool.Create(Delegate, { SerializerOptions = ToolOptions })`, which also raised IL3050 (`Array.CreateInstance`) in the AOT build; (2) `AIFunctionFactory.Create` with `SerializerOptions = ToolOptions` directly. Both failed naming only the SDK's two resolvers, so the SDK was not dropping the options. Both were reverted; `WithTools<T>(ToolOptions)` stays.
- Criterion 1, Windows (vswhere on PATH): `dotnet publish Curl.Mcp -r win-x64 -p:PublishAot=true -o Z:\tmp\lane1-aot` printed no IL or trim warning and made a native `Curl.Mcp.exe` of 30,565,888 bytes. Over stdio it answered `initialize` (`serverInfo` Curl.Mcp 0.1.0.0, protocol 2025-06-18), and `tools/list` listed build_url, curl, parse_url, transfer and version. `parse_url`, `version` and `curl --version` returned results, and a `transfer` with one header and a body ran (to a closed port it returned a CurlExit failure, as expected). Against a loopback server, `CurlMcpNativeAotIntegrationTests.NativeBinary_TransferWithOneHeader_SendsItAndReturnsTheResponse` passes: it sees `X-Aot: yes` on the wire and gets 200 with the body back.
- Integration: `dotnet test Curl.Mcp.IntegrationTests --filter CurlMcpNativeAot|CurlMcpServer` gave 10 passed in 52 s (the 3 new AOT tests included). There is no `[OSCondition]`: the test adds vswhere to PATH itself on Windows, and Linux and macOS runners carry clang and Xcode. If ubuntu-latest ever lacks zlib1g-dev, add `[OSCondition]` or install it in ci.yml.
- Packing decision (ADR-0621): `PublishAot` is passed as `-p:PublishAot=true`, not set in the csproj. With it, a plain `dotnet pack` makes only the tool package (verified: `Curl.Mcp.0.1.0.nupkg`, 5,869 bytes, DotnetToolSettings listing the six runtime packages). `dotnet pack -r win-x64 -p:PublishAot=true` makes `Curl.Mcp.win-x64.0.1.0.nupkg` holding the native `tools/any/win-x64/Curl.Mcp.exe` and no `Curl.Mcp.dll`. `ci.yml`'s guarded `mcp-pack` job expects seven packages from one plain pack, so setting it in the csproj would turn CI red, and a lane may neither edit ci.yml nor file a task touching it.
- `release.yml` `pack-mcp` is now a 7-leg matrix: a tool-package leg plus one leg per runtime on its own OS (the same runners as the curl publish). Each leg checks its one package: server.json at the version, or the native binary present and no Curl.Mcp.dll. Each runtime leg except osx-x64 extracts the binary and checks it answers `initialize`. The check script ran locally for `any` and `win-x64` and passed. Closing stdin at once races the reply (the server stops at end of input before answering), so the smoke test keeps stdin open until it has read the answer.
- **For an interactive session (guard file):** change `ci.yml`'s `mcp-pack` job to the per-OS AOT shape of `release.yml`'s `pack-mcp` (pass `-p:PublishAot=true`, pack `-r <rid>` for the runner's runtimes, and expect 1 + N packages rather than 7). Then set `<PublishAot>true</PublishAot>` in `Curl.Mcp.csproj` and update the comment there, ADR-0621 decision 2 and `Curl.Mcp/README.md`'s Package section. The root CLAUDE.md needs no change: the package exception is unchanged.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Mcp,Curl.Mcp.UnitLibrary` gave 0 failing members. Fast tests: all green (Curl.Mcp.UnitTests 476 passed, including the new `CurlMcpJsonContextTests`, which hold every tool contract resolvable from the chain without reflection).

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. Curl.Mcp publishes and runs as native AOT: ToolOptions built on first use; release.yml packs each runtime natively on its own OS
