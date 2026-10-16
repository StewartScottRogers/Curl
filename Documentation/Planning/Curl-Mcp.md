# Curl.Mcp: Curl as a Model Context Protocol server

Status: planned. Tasks BL-2376 to BL-2393 are filed on the board, all behind BL-2321 (the
last Libcurl and Curl.Api phase 1 and 2 task). Design: [ADR-0548](Decisions/ADR-0548-curl-mcp-an-mcp-server-over-the-api-seam-on-microsofts-sdk.md);
the seam it stands on: [ADR-0506](Decisions/ADR-0506-libcurl-and-curl-api-over-one-transfer-engine.md) and [Curl-Api-And-libcurl.md](Curl-Api-And-libcurl.md).

## What it is

A fourth front end over the one transfer engine, beside `Curl.Console`, `Libcurl` and
`Curl.Api`: an MCP server over stdio that AI agents (GitHub Copilot in VS Code and Visual
Studio, Claude, any MCP client) use as a tool. It is built on Microsoft's C# MCP SDK
(`ModelContextProtocol` 2.2.0 and `Microsoft.Extensions.Hosting` 10.0.x, both approved by
Stewart on 2026-10-15 and referenced by `Curl.Mcp` only) and packed the way Microsoft
documents it (<https://learn.microsoft.com/dotnet/ai/quickstarts/build-mcp-server>): a NuGet
package of `PackageType` `McpServer` that `dnx Curl.Mcp` runs.

## Projects

| Project | Holds |
| --- | --- |
| `Curl.Mcp` | The thin host: generic host, stdio transport, `[McpServerToolType]` classes whose methods forward to one `Curl.Mcp.UnitLibrary` call each, the `InProcessCurl` runner adapter, packing. The only project with the SDK packages. |
| `Curl.Mcp.UnitLibrary` | All tool logic, base class library only: path confinement, result and error plumbing, the tool implementations over `Curl.Api.UnitLibrary`'s seam. |
| `Curl.Mcp.UnitTests` | Tests both. Held to the four quality gates like `Curl.Api`. |
| `Curl.Mcp.IntegrationTests` | Starts the real server over stdio (ADR-0421 rules). |

## Tools and resources

| Item | Same as | Built on |
| --- | --- | --- |
| tool `curl` (argument array, optional stdin; stdout, stderr, exit code) | the `curl` executable | `Curl.Console.InProcessCurl` through `ICurlCommandRunner` |
| tool `transfer` (url, method, headers, body, options; status, headers, body, `TransferInfo`) | `Curl.Api` | `ICurlEngine`, `IEasyTransfer`, `EasyOptions` |
| tools `parse_url`, `build_url` | `CurlUrlHandle` | `ICurlUrlHandle` |
| tool `version` | `curl -V` | `CurlVersionInfo` |
| resources `curl://ai-help`, `curl://ai-help/all`, `curl://ai-help/<category>` | `--ai-help` | `CurlAiHelpText` |

Failures are MCP tool errors carrying curl's own message and exit code. Bodies that are not
UTF-8 are returned as base64. What a tool returns is capped (`CURL_MCP_MAX_RESPONSE_BYTES`,
default 1 MiB) and truncation is always stated.

## File access is confined

An agent driving curl can read and write files, so every path a tool would use must resolve
inside `CURL_MCP_ROOT` (symlinks and `..` resolved), or the tool is refused before any
transfer starts, with an error naming the option. The rule is built in three steps: a
resolver, a classification of every path-carrying option derived from
`Curl.Cli.UnitLibrary`'s option table (with a drift test that fails when a new option is
unclassified; unknown means refused), and a checker for whole argument arrays that also
finds paths embedded in values (`-d @file`, `-F name=@file`, `-w @file`, `file://`, `-K`
configs). Network access is not restricted.

## Tasks

Every task also depends on BL-2321.

| ID | Task | Depends on |
| --- | --- | --- |
| BL-2376 | Scaffold the three projects, packages, solution entries, quality-gate wiring, empty stdio host | |
| BL-2377 | Path resolver (`CURL_MCP_ROOT`) | 2376 |
| BL-2378 | Classify every path-carrying option | 2377 |
| BL-2379 | Check a whole argument array, embedded paths and `file://` | 2378 |
| BL-2380 | Tool errors, base64 or text, size cap, cancellation | 2376 |
| BL-2381 | `curl` tool logic | 2379, 2380 |
| BL-2382 | `curl` tool wiring over `InProcessCurl` | 2381 |
| BL-2383 | `transfer` tool logic | 2380 |
| BL-2384 | `transfer` tool wiring over the managed engine | 2382, 2383 |
| BL-2385 | `parse_url` and `build_url` | 2376, 2380 |
| BL-2386 | `version` | 2376, 2380 |
| BL-2387 | `--ai-help` resources | 2376 |
| BL-2388 | Pack as a NuGet MCP server, `.mcp/server.json` | 2382, 2387 |
| BL-2389 | Native AOT attempt, ADR with the outcome | 2388 |
| BL-2390 | `Curl.Mcp.IntegrationTests` (stdio, loopback `curl`, confinement, pack) | 2384 to 2388 |
| BL-2391 | CI packs on three platforms (guard file; interactive only) | 2388 to 2390 |
| BL-2392 | README, `mcp.json` setup, root README row, glossary, root `CLAUDE.md` | 2389, 2390 |
| BL-2393 | Publish to nuget.org (Stewart supplies the API key; interactive only) | 2391, 2392 |

## Packing

`dotnet pack Curl.Mcp`: `PackAsTool`, `PackageType` `McpServer`, `.mcp/server.json`
(`name` `io.github.StewartScottRogers/Curl`, `registryType` `nuget`, `CURL_MCP_ROOT`
required), self-contained per runtime identifier, native AOT where the SDK allows it
(BL-2389 decides and records why if not). Publishing is Stewart's release step (BL-2393).

## Not planned

HTTP transport (stdio only for now), and any restriction on which hosts a tool may reach.
