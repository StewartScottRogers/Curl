---
id: BL-4098
title: Pass extensions to Curl.Api and Curl.Mcp transfers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4091]
touches: [Curl.Api, Curl.Api.UnitLibrary, Curl.Api.UnitTests, Curl.Mcp, Curl.Mcp.UnitLibrary, Curl.Mcp.UnitTests]
requirement: none
created: 2026-10-29
completed:
---
# BL-4098 — Pass extensions to Curl.Api and Curl.Mcp transfers

## Goal

Transfers run through `Curl.Api` and `Curl.Mcp` (via `ManagedCurlEngine`) reach the same extensions as the `curl` tool, and `CURL_EXTENSIONS=0` turns them off there too.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". ADR-0999 wired only `Curl.Console`; `Libcurl` stays without extensions to match real libcurl.

## Acceptance criteria

- [ ] A `Curl.Api` unit test runs a transfer to a test extension's scheme and reaches its handler.
- [ ] A `Curl.Mcp` unit test does the same through the MCP transfer tool.
- [ ] With `CURL_EXTENSIONS=0` both refuse the scheme as an unsupported protocol (unit tests).
- [ ] `Libcurl` still references no extension project (the isolation build check passes).
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes



## Log

- 2026-10-29: Created.
