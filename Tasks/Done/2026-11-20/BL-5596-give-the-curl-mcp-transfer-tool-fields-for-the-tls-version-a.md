---
id: BL-5596
title: Give the curl-mcp transfer tool fields for the TLS version and a CA file
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Mcp.UnitLibrary/McpTransferOptions.cs, Curl.Mcp.UnitLibrary/McpTransferRequest.cs, Curl.Mcp.UnitLibrary/McpTransferTool.cs, Curl.Mcp.UnitLibrary/McpTransferRequestCheck.cs, Curl.Mcp/CurlMcpHost.cs, Curl.Mcp/Tools/TransferMcpTool.cs, Curl.Mcp/README.md, Curl.Mcp.UnitTests]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-5596 — Give the curl-mcp transfer tool fields for the TLS version and a CA file

## Goal

An agent can set the minimum TLS version and a CA file (confined to `CURL_MCP_ROOT`) on the `curl-mcp` `transfer` tool, without falling back to the `curl` tool.

## Context

From Surly cookbook bug report `2026-11-19T1942-Curl-bug-report.md` (batch 1 of the 1,000-recipe plan), finding #3. Build tested by the reports: the Curl port at `b9ca725077` (`curl`, `curl-mcp 0.1.0`, `Curl.Api 0.2.0`, `Libcurl 0.2.0`), Windows 11 (Linux and macOS through the cookbook's CI where stated); server Surl `763754785`. Check that it still reproduces on the current branch first.

`transfer` has no field for the TLS version or `--cacert`, so agents testing a self-signed server must fall back to the `curl` tool. Add fields mapping to `--tlsv1.2`/`--tlsv1.3` (or a `minTlsVersion` value) and `--cacert`, the CA path checked against `CURL_MCP_ROOT` like every other path. Update the tool's JSON schema and its description, and the MCP docs.

## Acceptance criteria

- [x] The `transfer` tool's schema has a TLS-version field and a CA-file field, described in the tool description.
- [x] A transfer to an HTTPS test server with a self-signed certificate succeeds with the CA field naming that certificate inside the root, and fails with exit 60 without it.
- [x] A CA path outside `CURL_MCP_ROOT` is refused with `PathRefused`.
- [x] Focused unit tests in `Curl.Mcp.UnitTests` pin each case; `dotnet build` is clean and the fast tests pass.

## Notes

- Added fields `minTlsVersion` (1.0-1.3, validated) and `caFile` (checked with McpConfinementRoot, refused as PathRefused outside it or with no root). McpTransferTool takes the root as an optional third constructor argument; the host passes it. touches widened to the host files and check/tests, which no other Doing task holds.
- The self-signed exit 60 / success pair is pinned with the fake engine (the CA reaches CertificateAuthorityFile; exit 60 carried through); no ADR, a small field addition. Quality: Curl.Mcp and Curl.Mcp.UnitLibrary 100%/100%, no failing member. Fast tests 590 pass.

## Log

- 2026-11-20: Created from Surly bug reports (1942 #3).
- 2026-11-20: Backlog -> Doing. Criteria at claim: 5976936f 77dcbba3 5dc5012e c5fa87fb.
- 2026-11-20: Doing -> Done. curl-mcp transfer tool has minTlsVersion and caFile (confined to CURL_MCP_ROOT) fields
