---
id: BL-5688
title: Keep the transfer tool's maxBodyBytes within the operator's response cap and state the cap truly
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Mcp.UnitLibrary/McpTransferTool.cs, Curl.Mcp/Tools/TransferMcpTool.cs, Curl.Mcp.UnitTests, Curl.Mcp.IntegrationTests/Golden, Documentation/Planning/Decisions]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-5688 — Keep the transfer tool's maxBodyBytes within the operator's response cap and state the cap truly

## Goal

A `transfer` call's `maxBodyBytes` can no longer lift the body cap above a lower limit the operator set with `CURL_MCP_MAX_RESPONSE_BYTES`, and the tool's description and schema state the default cap truthfully.

## Context

Found by the shift-end review of 2026-11-21 (ADR-0710; silent-failure-hunter and type-design-analyzer rated it Important, code-reviewer concurred) on the BL-5671 change (ADR-1330).

- `Curl.Mcp.UnitLibrary/McpTransferTool.cs:50` builds `new McpResponseCap(maxBodyBytes)` in place of the server's `cap`. An operator who sets `CURL_MCP_MAX_RESPONSE_BYTES=65536` to bound memory and agent context finds any client can still ask for up to `McpResponseCap.CeilingMaxBytes` (64 MiB) per call; the server-side limit became a mere default without anyone deciding that.
- `Curl.Mcp/Tools/TransferMcpTool.cs:23` (description) and the `maxBodyBytes` schema entry (line ~62) say the limit is "1 MiB (1048576 bytes) by default" / "the server's limit, 1048576". That is false whenever `CURL_MCP_MAX_RESPONSE_BYTES` is set - a "say what it does" defect. `McpTransferRequest.cs`'s doc comment already words it correctly ("1 MiB unless `CURL_MCP_MAX_RESPONSE_BYTES` sets another").

Suggested rule: when `CURL_MCP_MAX_RESPONSE_BYTES` is set, it is the ceiling for `maxBodyBytes` (a larger value is clamped or refused `InvalidRequest`); when it is unset, the ceiling stays 64 MiB. Decide which, and record it as an amendment to ADR-1330.

## Acceptance criteria

- [x] With `CURL_MCP_MAX_RESPONSE_BYTES` set below the requested `maxBodyBytes`, a unit test in `Curl.Mcp.UnitTests` shows the returned body is no longer than the operator's limit (or the call is refused `InvalidRequest` naming `maxBodyBytes`).
- [x] With the variable unset, a unit test shows `maxBodyBytes` up to 64 MiB still raises the cap above 1 MiB.
- [x] The tool description and the `maxBodyBytes` schema text say the default is the server's limit (1 MiB unless `CURL_MCP_MAX_RESPONSE_BYTES` sets another) and how the operator's limit bounds the field; the goldens in `Curl.Mcp.IntegrationTests/Golden` match.
- [x] ADR-1330 (or a new ADR that amends it) records the rule.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing. Criteria at claim: 16e59f1e 9fdb3919 22e83cd6 2ee404e4 3d836d5e.
- 2026-11-21: Doing -> Done. Clamped maxBodyBytes to the operator limit; text, goldens, ADR-1330 amendment done
