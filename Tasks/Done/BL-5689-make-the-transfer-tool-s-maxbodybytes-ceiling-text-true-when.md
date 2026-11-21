---
id: BL-5689
title: Make the transfer tool's maxBodyBytes ceiling text true when CURL_MCP_MAX_RESPONSE_BYTES exceeds 64 MiB
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Mcp/Tools/TransferMcpTool.cs, Curl.Mcp.UnitLibrary/McpTransferRequestCheck.cs, Curl.Mcp.UnitLibrary/McpResponseCap.cs, Curl.Mcp.UnitTests/Tools/TransferMcpToolTests.cs, Curl.Mcp.UnitTests/McpTransferProxyAndLimitFieldsTests.cs, Curl.Mcp.IntegrationTests/Golden/tools-list.json, Curl.Mcp.IntegrationTests/Golden/request-before-initialize.json, Documentation/Planning/Decisions/ADR-1330-the-transfer-tool-takes-proxy-login-proxy-ca-resolve-and-a-body-limit-field.md]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-5689 — Make the transfer tool's maxBodyBytes ceiling text true when CURL_MCP_MAX_RESPONSE_BYTES exceeds 64 MiB

## Goal

The `transfer` MCP tool's description, its `maxBodyBytes` schema text, ADR-1330's amendment and the code agree on the ceiling for `maxBodyBytes` when `CURL_MCP_MAX_RESPONSE_BYTES` is set above 64 MiB.

## Context

Found by the shift-end review (ADR-0710) of 976e730b0f..826d045749, which merged BL-5688. The finding came from the pr-review-toolkit code-reviewer.

What BL-5688 changed:
- It added `McpResponseCap.RequestCeilingBytes` (`Curl.Mcp.UnitLibrary/McpResponseCap.cs:33`), the operator's limit when the variable is set.
- `McpTransferTool.RunAsync` (`Curl.Mcp.UnitLibrary/McpTransferTool.cs:50`) clamps `maxBodyBytes` to that limit.
- It rewrote the text in three places: the tool description (`Curl.Mcp/Tools/TransferMcpTool.cs:23`), the `maxBodyBytes` schema text (`Curl.Mcp/Tools/TransferMcpTool.cs:60`) and the ADR-1330 amendment. All three say that when the variable is set, `maxBodyBytes` can go "up to that variable's value", and that a larger value is clamped, "not refused".

Why the text is false: `McpTransferRequestCheck.CheckMaxBodyBytes` (`Curl.Mcp.UnitLibrary/McpTransferRequestCheck.cs:176`) still refuses any `maxBodyBytes` above `McpResponseCap.CeilingMaxBytes` (64 MiB) with `InvalidRequest`.

Example: set `CURL_MCP_MAX_RESPONSE_BYTES=134217728` (128 MiB) and call with `maxBodyBytes=100000000`. The text promises up to 128 MiB, but the call is refused. The real ceiling is the smaller of 64 MiB and the variable's value. The schema's "1 to 67108864" range is also stated next to "up to that variable's value" without saying which one wins.

This breaks the repository's "say what it does, do what it says" rule. Decide which way to fix it, by code or by text, under Stewart's delegation:
- Code: let `CheckMaxBodyBytes` accept up to `RequestCeilingBytes` when it is larger.
- Text: state "up to the smaller of 64 MiB and CURL_MCP_MAX_RESPONSE_BYTES".

Record the choice in ADR-1330's amendment.

## Acceptance criteria

- [x] The tool description, the `maxBodyBytes` schema text and ADR-1330's amendment state the same ceiling the code enforces, including when `CURL_MCP_MAX_RESPONSE_BYTES` is above 67108864.
- [x] A unit test in `Curl.Mcp.UnitTests` sets the variable above 64 MiB and pins what a `maxBodyBytes` between 64 MiB and that value does: a refusal with `InvalidRequest`, or a body returned up to the requested size.
- [x] `TransferMcpToolTests` and the two goldens (`tools-list.json`, `request-before-initialize.json`) match the new text.
- [x] `dotnet build` is clean, and the fast tests pass with Curl.Mcp and Curl.Mcp.UnitLibrary still at 100% line and branch coverage.

## Notes

Decided: fix the text, not the code (ADR-1330 amendment): the real ceiling is the smaller of 64 MiB and the variable. Only a doc comment changed in production code, so no quality run. Unit tests pass (661).

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing. Criteria at claim: c9cfd04c 44ac0007 c3da6b19 9134e538.
- 2026-11-21: Doing -> Done. Text fixed: ceiling is the smaller of 64 MiB and the variable; unit test pins refusal.
