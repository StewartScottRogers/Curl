# ADR-0596: Curl.Mcp packs as a NuGet MCP server, one self-contained package per runtime

- Status: Accepted
- Date: 2026-10-16
- Task: BL-2388
- Decided by Claude under Stewart's delegation.

## Context

ADR-0548 decides that `Curl.Mcp` ships as a NuGet package of `PackageType` `McpServer` that
`dnx Curl.Mcp` runs, packed the way Microsoft's `mcpserver` template does it. Two things in this
solution differ from the template: `Curl.Mcp` references `Curl.Console`, an executable, for its
`InProcessCurl`; and `Directory.Build.props` holds no package metadata.

## Decision

- `Curl.Mcp.csproj` sets `PackAsTool`, `PackageType` `McpServer`, `ToolCommandName` `curl-mcp`,
  `PublishSelfContained` and `RuntimeIdentifiers` `win-x64;win-arm64;linux-x64;linux-arm64;osx-arm64;osx-x64`
  (the task's list; the template's `linux-musl-x64` is left out and `osx-x64` added). `dotnet pack`
  makes a pointer package `Curl.Mcp` and one self-contained `Curl.Mcp.<rid>` package per runtime.
- The version is `0.1.0`, set as `Version` in the csproj and repeated in `.mcp/server.json`
  (both `version` fields); `McpPackagingTests` holds them equal. Publishing moves it (BL-2393).
- `.mcp/server.json` follows the template's 2025-10-17 registry schema: name
  `io.github.StewartScottRogers/Curl`, one `nuget` package `Curl.Mcp` over `stdio`, and
  `environmentVariables` `CURL_MCP_ROOT` (`isRequired` true) and `CURL_MCP_MAX_RESPONSE_BYTES`
  (`isRequired` false), with no field the schema lacks.
- Package metadata (authors, licence, project and repository URL) is set in `Curl.Mcp.csproj`,
  because `Directory.Build.props` has none and only this project packs; the licence is the root
  `LICENSE.txt`, packed as a licence file because curl's licence has no SPDX expression NuGet takes.
- `Curl.Console` is referenced with `GlobalPropertiesToRemove="RuntimeIdentifier;SelfContained"`
  and `ValidateExecutableReferencesMatchSelfContained` false: it is used as a library, never run
  as its own executable, so building it runtime-agnostic lets each per-runtime pack proceed
  without a per-runtime restore of `Curl.Console` (NETSDK1047) and without NETSDK1150.

## Consequences

`dotnet pack Curl.Mcp -c Release` produces seven packages, about 40 MB per runtime. Native AOT for
the per-runtime packages is BL-2389's question; CI packing on three platforms is BL-2391's.
