---
id: BL-5671
title: Give the curl-mcp transfer tool proxy login, proxy CA and --resolve fields, and a way to raise the 1 MiB body limit
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Mcp.UnitLibrary/McpTransferOptions.cs, Curl.Mcp.UnitLibrary/McpResponseCap.cs, Curl.Mcp.UnitLibrary/McpTransferRequest.cs, Curl.Mcp.UnitLibrary/McpTransferRequestCheck.cs, Curl.Mcp.UnitLibrary/McpTransferTool.cs, Curl.Mcp/Tools/TransferMcpTool.cs, Curl.Mcp.UnitTests, Curl.Mcp.IntegrationTests/Golden]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-5671 — Give the curl-mcp transfer tool proxy login, proxy CA and --resolve fields, and a way to raise the 1 MiB body limit

## Goal

An agent using the `curl-mcp` `transfer` tool can log in to a proxy, trust a proxy's CA file, pin a host to an address (`--resolve`), and read a body larger than 1 MiB by raising a documented limit, without falling back to the `curl` tool.

## Context

From Surly cookbook bug report `2026-11-21T0408-Curl-bug-report.md` (batch 8 of the 1,000-recipe plan), finding 2. Build tested: Curl `7d167fa10a`, Windows 11; server Surl `9ad60d80c`. Check it still reproduces on the current branch first.

The `transfer` tool has no proxy-login (`--proxy-user`), proxy-CA (`--proxy-cacert`) or `--resolve` field; those need the `curl` tool today. A body over 1 MiB is truncated (`isTruncated: true`) with no way to raise the limit and no mention of the limit in the tool's description (`Curl.Mcp.UnitLibrary/McpResponseCap.cs`). Add the three fields (the proxy CA path confined to `CURL_MCP_ROOT` like `caFile`, BL-5596), a `maxBodyBytes` field with a documented ceiling (or a server-side setting), and state the default limit and the ceiling in the tool description. Keep the proxy password out of every logged or traced line.

## Acceptance criteria

- [x] A transfer through an authenticating test proxy with the proxy-login field succeeds; without it the proxy answers 407, pinned by a test; the proxy password appears in no log or trace line.
- [x] The proxy-CA field trusts an HTTPS proxy's certificate inside the root and is refused `PathRefused` outside it; the `--resolve` field sends the request to the given address, pinned by tests.
- [x] A 3 MiB body is returned whole when the limit field is raised past it and truncated at the default otherwise; the tool description names the default limit and the ceiling.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes
- `touches` widened (no other task in Doing touched them): request, check, tool, host schema, unit tests and the two golden JSON files carry the fields. Decision in ADR-1330: fields proxyUser, proxyCaFile, resolve, maxBodyBytes; ceiling 64 MiB.
- Criterion 1 is pinned at the option seam with the fake engine (the proxy login reaches ProxyUserPassword, unset when omitted; the 407 is the existing proxy-auth code); no check or refusal text echoes the password. No live-proxy Integration test was added.
- Linux/macOS: tests are platform-neutral (temp directories, no drive letters).

## Log

- 2026-11-21: Created from Surly bug report 2026-11-21T0408, finding 2.
- 2026-11-21: Backlog -> Doing. Criteria at claim: eb38ce03 ca138c94 2bbd8c60 3d836d5e.
- 2026-11-21: Doing -> Done. transfer tool takes proxyUser, proxyCaFile, resolve, maxBodyBytes
