---
id: BL-4906
title: Send credentials and custom headers on mcp:// and mcps://, and report a persistent 401 as exit 67
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.McpClient.UnitLibrary/StreamableHttp/McpStreamableHttpTransport.cs, Curl.Extended.Protocol.McpClient.UnitTests/McpAuthorizationHeaderTests.cs, Documentation/Conformance/Extended/McpClient.md]
requirement: none
created: 2026-11-11
completed:
---
# BL-4906 — Send credentials and custom headers on mcp:// and mcps://, and report a persistent 401 as exit 67

## Goal

Every MCP Streamable HTTP request carries `--oauth2-bearer`, `-u`, `-H` and `-A` as curl would send them on HTTP, and a 401 that persists exits 67.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding EXT-003 (High, confirmed: reproduced at least twice).

- Repro: MCP Streamable-HTTP mock on 18710 whose `/secure` answers 401 with `WWW-Authenticate: Bearer resource_metadata=...` unless `Authorization: Bearer tok1` is present, otherwise serves `initialize` and `tools/list` (tools `a` "tool A", `b` "tool B"), logging each Authorization header. `curl.exe -sS --oauth2-bearer tok1 mcp://127.0.0.1:18710/secure`; same with `-H "Authorization: Bearer tok1"`; `-u user:pw`; `-H "X-Api-Key: k1" -A agent`.
- Expected: MCP 2025-06-18 Authorization: every request carries `Authorization: Bearer <token>` (as the port's grpc sends `-H` and az/gs honour `--oauth2-bearer`); a persistent 401 is an authentication failure (67).
- Actual: None of 15 logged requests carries Authorization, the custom header or the User-Agent; `curl: (8) The server answered with status 401.`, exit 8.
- Where to start: `McpStreamableHttpTransport.cs` ~line 187 builds the request head from fixed headers only.

## Acceptance criteria

- [ ] The `--oauth2-bearer` repro prints `a<TAB>tool A` and `b<TAB>tool B`, exits 0, and every request carries `Bearer tok1`; same with `-H`.
- [ ] `-A agent` appears as User-Agent and `-H "X-Api-Key: k1"` is sent.
- [ ] Without a token the exit is 67, not 8.
- [ ] `Documentation/Conformance/Extended/McpClient.md` has Authorization rows.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: 3784074a 490972ef 03a003fc 218ad207 a0b49a29.
