---
id: BL-4906
title: Send credentials and custom headers on mcp:// and mcps://, and report a persistent 401 as exit 67
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.McpClient.UnitLibrary/StreamableHttp/McpStreamableHttpTransport.cs, Curl.Extended.Protocol.McpClient.UnitLibrary/StreamableHttp/McpRequestHeaderFields.cs, Curl.Extended.Protocol.McpClient.UnitLibrary/Client/McpHttpSession.cs, Curl.Extended.Protocol.McpClient.UnitLibrary/Client/McpProtocolHandler.cs, Curl.Extended.Protocol.McpClient.UnitTests/McpAuthorizationHeaderTests.cs, Curl.Extended.Protocol.McpClient.UnitTests/Client/McpProtocolHandlerTests.cs, Documentation/Conformance/Extended/McpClient.md, Curl.Conformance.Extended.UnitLibrary/McpClient/McpStreamableHttpReferenceServer.cs, Curl.Conformance.Extended.UnitTests/McpClient/McpAuthorizationCases.cs]
requirement: none
created: 2026-11-11
completed: 2026-11-11
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

- [x] The `--oauth2-bearer` repro prints `a<TAB>tool A` and `b<TAB>tool B`, exits 0, and every request carries `Bearer tok1`; same with `-H`.
- [x] `-A agent` appears as User-Agent and `-H "X-Api-Key: k1"` is sent.
- [x] Without a token the exit is 67, not 8.
- [x] `Documentation/Conformance/Extended/McpClient.md` has Authorization rows.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

- Decision (ADR-1238): `McpRequestHeaderFields` builds, after `Host`, `Authorization: Bearer` from `--oauth2-bearer` (else Basic from `-u`), `User-Agent` (`-A`, curl/8.22.0 by default, none for `-A ""`) and each `-H` with curl's replace/remove/empty rules; a 401 ends with exit 67, other statuses stay 8. MCP is an extension, so there is no real-curl output to measure; the error text is the existing "The server answered with status 401.".
- Touches widened (no task in Doing on origin/work/dark-factory names these): McpRequestHeaderFields.cs (new), McpHttpSession.cs (wires the context in), McpProtocolHandler.cs (exit 67), McpProtocolHandlerTests.cs (one head assertion now has User-Agent), and the reference server and the new McpAuthorizationCases.cs in the Conformance.Extended projects, because the requirement matrix test accepts only `[SpecRequirement]` conformance cases for the Authorization rows.
- Criteria 1 to 3 verified end to end by McpAuthorizationCases, which runs Curl.Console in process against the reference MCP server requiring `Bearer tok1`: `--oauth2-bearer tok1` and `-H "Authorization: Bearer tok1"` list the tools with exit 0 and every request carries the token; `-H "X-Api-Key: k1" -A agent` sends both; without a token the exit is 67. Unit tests in McpAuthorizationHeaderTests.cs.
- Environment: this machine has no .NET SDK 10.0.401 (only 10.0.100-rc.1, 11.0.100-rc.2, 11.0.100), so global.json's pin fails; builds, tests and Measure-CodeQuality ran from a temp folder whose own global.json selects SDK 11.0.100 (targets stay net10.0). `dotnet format` could not run (it resolves the repo's global.json); the build's analyzers passed. Measure-CodeQuality: McpClient 100% line and branch, 0 failing; Conformance.Extended's only failing member is Mqtt5WebSocketReferenceServer.Collect (not touched here).
- Fast tests: every MCP and conformance project is green. Six Windows SSPI NTLM tests fail on this machine (Ldap, Authentication, Console: Begin_NtlmOnSspi..., RunAsync_WindowsProductionRoute...); they do not touch MCP, and BL-4899 in Doing works on LDAP/NTLM. The changes are platform-neutral (no paths, OS text or certificates), so CI on Linux and macOS is covered by the same tests.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: 3784074a 490972ef 03a003fc 218ad207 a0b49a29.
- 2026-11-11: Doing -> Done. mcp:// and mcps:// send --oauth2-bearer, -u, -H and -A on every request, and a persistent 401 exits 67
