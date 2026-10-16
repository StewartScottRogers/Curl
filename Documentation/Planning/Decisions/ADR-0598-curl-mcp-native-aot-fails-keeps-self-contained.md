# ADR-0598: Curl.Mcp stays self-contained; native AOT builds but the server fails to start

- Status: Accepted
- Date: 2026-10-16
- Task: BL-2389
- Decided by Claude under Stewart's delegation.

## Context

ADR-0548 decision 6 asks for native AOT where the SDK allows it, and self-contained publishing
if it does not (recorded here with the reason). ADR-0596 packs `Curl.Mcp` self-contained per
runtime identifier.

## What was tried

The exact command, with `C:\Program Files (x86)\Microsoft Visual Studio\Installer` on PATH, SDK 10.0.402,
`ModelContextProtocol` 2.2.0, `Microsoft.Extensions.Hosting` 10.0.13:

```
dotnet publish Curl.Mcp -r win-x64 -p:PublishAot=true -o <folder>
```

- The build succeeds: no trim or AOT warning, and `Curl.Mcp.exe` is a native binary of 30,222,336 bytes.
- Running it fails before it answers `initialize`. The host stops with
  `System.NotSupportedException: JsonTypeInfo metadata for type 'Curl.Mcp.McpTransferHeader[]' was not provided by TypeInfoResolver of type '[ModelContextProtocol.McpJsonUtilities+JsonContext, Microsoft.Extensions.AI.AIJsonUtilities+JsonContext]'`,
  thrown from `AIFunctionFactory.ReflectionAIFunctionDescriptor.GetParameterMarshaller` under
  `AIFunctionMcpServerTool.Create`, while the `transfer` tool's parameters are marshalled.
- `CurlMcpHost` already passes `CurlMcpJsonContext.ToolOptions` (source-generated, with `McpTransferHeader[]`
  registered) to every `WithTools<T>`; the SDK's marshaller reports only its own two resolvers, so in 2.2.0
  the options do not reach parameter marshalling on the AOT path. `tools/list` was therefore never reached.

## Decision

`PublishAot` stays off. `Curl.Mcp` ships self-contained per runtime identifier, as ADR-0596 decides.
Native AOT is retried when `ModelContextProtocol` is upgraded: the check is the command above, then
`initialize` and `tools/list` over stdio, which must list `curl`, `transfer`, `parse_url`, `build_url`
and `version` (the last two exist once BL-2385 lands).
