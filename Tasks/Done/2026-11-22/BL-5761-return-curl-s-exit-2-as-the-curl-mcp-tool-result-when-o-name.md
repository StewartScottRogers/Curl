---
id: BL-5761
title: Return curl's exit 2 as the curl-mcp tool result when -o names a folder that does not exist, not a plain tool error
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Mcp.UnitTests/InProcessCurlCommandRunnerTests.cs, Curl.Mcp.UnitTests/McpCurlToolMissingFolderTests.cs]
requirement: none
created: 2026-11-22
completed: 2026-11-22
---
# BL-5761 — Return curl's exit 2 as the curl-mcp tool result when -o names a folder that does not exist, not a plain tool error

## Goal

The `curl-mcp` `curl` tool with `-o` into a missing folder returns its normal JSON result carrying curl's exit 2 and message, as the console gives, instead of the plain text `An error occurred invoking 'curl'.`

## Context

From Surly cookbook bug report `2026-11-22T1308-Curl-bug-report.md` (batch 12 of the 1,000-recipe plan), finding 1. Build tested: Curl `c84035f797`, Windows 11; server Surl `5da5b3eaf`. That build predates BL-5758 (2026-11-22: every extension receives a Curl.Api body exactly as `-d`), so first check whether it still reproduces on the current branch; if it does not, pin it with a test and close.

Repro: `surl --mail-domain example.com --mailbox bob@example.com:bobpass smtp://127.0.0.1:2525/ pop3://127.0.0.1:1110/` with three messages for bob; the console `curl -sS -u bob@example.com:bobpass -o "nosuch/msg#1.eml" "pop3://127.0.0.1:1110/[1-3]"` (with `nosuch` missing) exits 2; the `curl-mcp` `curl` tool with the same arguments returns a tool error with the plain text `An error occurred invoking 'curl'.` (the generic text the MCP layer gives for an unhandled exception, `Curl.Mcp.Protocol.UnitLibrary/Tools/ToolsCallHandler.cs`). Find the exception the tool lets escape (likely the path check or output-file creation for a glob `-o` into a missing directory) and turn it into curl's own result: exit 2 (or whatever the console gives) with its stderr text. Recipe `pop3-drain-a-maildrop-from-an-agent`.

## Acceptance criteria

- [x] The `curl` tool with the repro's arguments returns a JSON result with the same exit code and stderr text as the console, pinned by a test.
- [x] No input to the `curl` tool produces the generic "An error occurred invoking" text for a failure curl itself reports; a test runs a handful of failing arguments (missing folder, unknown option, bad URL) and checks each returns a result with an exit code.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-22: Created from Surly bug report 2026-11-22T1308, finding 1.
- 2026-11-22: Backlog -> Doing. Criteria at claim: f9e4a29a d193aa6d 3d836d5e.
- 2026-11-22: Doing -> Done. Does not reproduce; pinned with tests
