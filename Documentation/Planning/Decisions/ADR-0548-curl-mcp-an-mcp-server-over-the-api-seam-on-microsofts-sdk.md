# ADR-0548 — `Curl.Mcp`: an MCP server over the API seam, on Microsoft's C# MCP SDK

- **Status:** Accepted
- **Date:** 2026-10-15
- **Status note (2026-10-23):** decision 1 (the two packages) and the SDK wording in decisions 2
  and 5 are superseded in intent by
  [ADR-0799](ADR-0799-curl-mcp-drops-its-two-packages-for-a-hand-written-server.md), which
  replaces the SDK with a hand-written server. The packages stay until its final task lands. The
  rest of this ADR stands.

`Curl.Mcp`, its two packages and publishing it to nuget.org were **approved by Stewart on
2026-10-15** ("built on Microsoft's SDK, yes, yes"). The other details below (the project
split, the tools, the file-access root, the transport) are **decided by Claude under
Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

Curl has three front ends over one transfer engine: the `curl` executable (`Curl.Console`
and `Curl.Cli.UnitLibrary`), `Libcurl` (libcurl's API, call for call) and `Curl.Api` (the
.NET-normal API), all standing on the seam in `Curl.Api.UnitLibrary` (ADR-0506). Stewart
asked for a fourth: `Curl.Mcp`, wrapping that seam as a Model Context Protocol server, so AI
agents (GitHub Copilot in VS Code and Visual Studio, Claude, any MCP client) can use Curl as
a tool, and built and packed the way Microsoft documents it
(<https://learn.microsoft.com/dotnet/ai/quickstarts/build-mcp-server>): the official C# MCP
SDK, packed as a NuGet MCP server that `dnx` runs and nuget.org lists under
`packagetype=mcpserver`.

## Decision

1. **Packages.** `ModelContextProtocol` (2.2.0, the latest stable on 2026-10-15) and
   `Microsoft.Extensions.Hosting` (the 10.0 line), approved by Stewart. They are referenced by
   `Curl.Mcp` only. No other project, and no `*.UnitLibrary`, takes them.
2. **Three projects, the `Curl.Api` pattern:**
   - `Curl.Mcp`: the thin host. It holds the SDK wiring only: the generic host, the stdio
     transport, and `[McpServerToolType]` classes whose methods each forward to one
     `Curl.Mcp.UnitLibrary` call. Moving to another MCP SDK changes only this project.
   - `Curl.Mcp.UnitLibrary`: the tool logic, base class library only, over
     `Curl.Api.UnitLibrary`'s seam and `Curl.Engine.UnitLibrary`.
   - `Curl.Mcp.UnitTests`: tests both.

   `Curl.Mcp` is a fourth exception to the `Curl.<Area>.UnitLibrary` naming rule, beside
   `Curl.Console`, `Curl.Api` and `Libcurl`, and is held to the four quality gates.
3. **What it provides, the same as the other front ends:**

   | MCP item | Same as |
   | --- | --- |
   | tool `curl`: any curl command line as an argument array, answered with stdout, stderr and the exit code | `curl` (Console and Cli): every option, the same bytes |
   | tool `transfer`: URL, method, headers, body and options, answered with status, headers, body and `TransferInfo` | `Curl.Api` |
   | tools `parse_url` and `build_url` | `CurlUrlHandle` |
   | tool `version`: version, protocols, features | `curl -V` |
   | resources: the `--ai-help` reference, one per category | `--ai-help` |

4. **File access is confined.** An agent driving curl can read and write files. Every path
   a tool would write (`-o`, `-O`, `--output-dir`, `-D`, `-c`, `--trace` and the like) or read
   (`-T`, `-d @file`, `-F name=@file`, `-K`, `file://`, `--cacert` and the like) must resolve
   inside one root folder, the `CURL_MCP_ROOT` environment variable, declared in
   `.mcp/server.json` as a required input. A path outside it is refused before the transfer
   starts, with a tool error naming the option. Network access is not restricted: reaching
   hosts is what the server is for.
5. **Transport.** Stdio, as `dnx` and the VS Code and Visual Studio clients run it. HTTP
   transport is later work, not planned as tasks.
6. **Packing.** `dotnet pack Curl.Mcp` produces the NuGet MCP server: `PackAsTool`,
   `PackageType` `McpServer`, `.mcp/server.json`, self-contained per runtime identifier, and
   native AOT where the SDK allows it, as Microsoft's `mcpserver` template does. If the SDK
   breaks the AOT build, `Curl.Mcp` is published self-contained instead, and the task that
   finds it records why. (ADR-0621: the released runtime packages are native AOT, packed with
   `-p:PublishAot=true` on each runtime's own OS by `release.yml`; a plain `dotnet pack` without
   it stays self-contained.)
7. **Publishing.** Stewart approved publishing `Curl.Mcp` to nuget.org. The push needs a
   nuget.org API key, which only Stewart holds; it is a release step, done when he supplies
   the key, never by a lane.

## Consequences

The solution has packages beyond `MSTest` for the first time; `CLAUDE.md`'s
base-class-library rule names the two and the one project that may use them. `Curl.Mcp`
waits for API phases 1 and 2 (the seam and the engine extraction). Its tasks are filed now,
behind BL-2321. The plan is in `Documentation/Planning/Curl-Mcp.md`.
