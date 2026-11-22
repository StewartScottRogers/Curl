---
id: BL-5754
title: Make a Curl.Api body to mcp://, mcps:// and mcp+stdio: call the tool, not list the tools
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.McpClient.UnitLibrary]
requirement: none
created: 2026-11-22
completed:
---
# BL-5754 — Make a Curl.Api body to mcp://, mcps:// and mcp+stdio: call the tool, not list the tools

## Goal

A `Curl.Api` `POST` with a `tools/call` JSON body to an `mcp://`, `mcps://` or `mcp+stdio:` URL makes that call and returns its result, as the console's `-d` does, instead of returning the tool list.

## Context

From Surly cookbook bug report `2026-11-22T1116-Curl-bug-report.md` (batch 11 of the 1,000-recipe plan), finding 2. Build tested: Curl `c84035f797`, Windows 11; server Surl `5da5b3eaf`. Check it still reproduces on the current branch first.

`surl mcp://127.0.0.1:8765/`; a `Curl.Api` `POST` to `mcp://127.0.0.1:8765/` with a `tools/call` JSON body returns the tools list (the extension''s no-data default). Same family as BL-5611 and BL-5681 (`Curl.Api` bodies not reaching extensions that take `-d`): the MCP client decides "list" when it sees no `-d` data, and the `Curl.Api` body does not arrive in the form it checks. Follow BL-5611's approach so the MCP client sees a `Curl.Api` byte, string or stream body as `-d` data. Cookbook recipe: `mcp-start-an-mcp-server-as-a-program-in-csharp`.

## Acceptance criteria

- [ ] Against Surl's MCP server (or the MCP test double), a `Curl.Api` `POST` with a `tools/call` body to `mcp://`, `mcps://` and `mcp+stdio:` returns the tool's result; the Notes record the runs.
- [ ] A unit test pins that a `Curl.Api` body reaches the MCP client as `-d` data for each scheme.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-22: Created from Surly bug report 2026-11-22T1116, finding 2.
