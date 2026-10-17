# ADR-0621: Curl.Mcp runs as native AOT once its JSON options are built on first use

- Status: Accepted
- Date: 2026-10-16
- Task: BL-2486
- Supersedes: ADR-0598's decision
- Decided by Claude under Stewart's delegation.

## Context

ADR-0598 found that `dotnet publish Curl.Mcp -r win-x64 -p:PublishAot=true` builds clean but the
native binary stops before `initialize` with `NotSupportedException: JsonTypeInfo metadata for type
'Curl.Mcp.McpTransferHeader[]' was not provided by TypeInfoResolver of type
'[ModelContextProtocol.McpJsonUtilities+JsonContext, Microsoft.Extensions.AI.AIJsonUtilities+JsonContext]'`,
and blamed `ModelContextProtocol` 2.2.0 for ignoring the options `CurlMcpHost` passes to `WithTools<T>`.
Stewart chose to work around it in Curl.Mcp rather than wait for an SDK upgrade, with a hand-written
MCP server as the fallback.

## What was found

The failure reproduces without AOT, in a minute rather than a publish: `dotnet build Curl.Mcp
-p:JsonSerializerIsReflectionEnabledByDefault=false` and the same `initialize` over stdio. With it:

- Registering every tool through `McpServerTool.Create(Delegate, new McpServerToolCreateOptions {
  SerializerOptions = CurlMcpJsonContext.ToolOptions })` failed the same way.
- Building each `AIFunction` with `AIFunctionFactory.Create` and `SerializerOptions =
  CurlMcpJsonContext.ToolOptions` directly, with no SDK code between, failed the same way, naming the
  same two resolvers. So the SDK was not dropping the options: `ToolOptions` itself lacked Curl's contracts.
- The cause is static initialization order. `ToolOptions` was a static auto-property initializer that
  passed `Default` to `JsonTypeInfoResolver.Combine`; the source generator sets `Default` in a static
  initializer in its own partial file, and the order in which the initializers of a partial class's files
  run is not defined. `ToolOptions` ran first, saw `Default` as `null`, and `Combine` drops a `null`
  resolver without complaint. Under the JIT the serializer's reflection fallback hid the gap; without
  reflection nothing covers it.

## Decision

1. `CurlMcpJsonContext.ToolOptions` is built on first use (`field ??= ...`), after every static
   initializer has run, so its chain is Curl's context, then the SDK's. The tools stay registered with
   `WithTools<T>(CurlMcpJsonContext.ToolOptions)`: no SDK workaround, no schema change, no new package,
   and the hand-written server fallback is not needed. `CurlMcpJsonContextTests` holds every tool
   parameter and result type resolvable from the chain alone, without reflection.
2. `Curl.Mcp.csproj` sets `<PublishAot>true</PublishAot>` (BL-2517), so every Curl.Mcp package is native.
   AOT cannot cross-compile between operating systems, so a plain `dotnet pack` makes only the tool package
   that points at the runtime packages, and each runtime's package needs `dotnet pack -r <rid>` on its own
   OS. `release.yml`'s `pack-mcp` job does exactly that, one leg per runtime on its own runner plus one for
   the tool package, and checks each runtime package holds its native binary and no `Curl.Mcp.dll`.
   `ci.yml`'s `mcp-pack` job does the same on its three runners: the runtime package of the runner's own
   runtime (win-x64, linux-x64, osx-arm64), whose extracted binary must answer `initialize`, and on Linux
   the tool package with `.mcp/server.json`. The seven self-contained packages of ADR-0596 are no longer
   made. (BL-2486 first passed `-p:PublishAot=true` on the command line only, because `ci.yml` is a guard
   file a lane may not change; BL-2517 moved the job and set the property.)
3. `Curl.Mcp.IntegrationTests` publishes the native binary for the machine's runtime and drives it over
   stdio: `initialize`, `tools/list` and a `transfer` call with one request header against a loopback
   listener (`CurlMcpNativeAotIntegrationTests`).

This supersedes ADR-0598's decision that `PublishAot` stays off until an SDK upgrade, and fulfils
ADR-0548 decision 6's native AOT for the published packages.

## Consequences

- The released `Curl.Mcp.<rid>` packages carry a native binary of about 30 MB that starts without the
  .NET runtime's JIT.
- A source-generated context's `Default` must never be read by a static initializer of the same class.
