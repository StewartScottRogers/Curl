---
id: BL-5516
title: Let curl-mcp write -o to the null device (NUL, /dev/null) under CURL_MCP_ROOT
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Mcp.UnitLibrary/McpOptionValuePaths.cs, Curl.Mcp.UnitTests/McpArgumentCheckerTests.cs]
requirement: none
created: 2026-11-18
completed: 2026-11-18
---
# BL-5516 — Let curl-mcp write -o to the null device (NUL, /dev/null) under CURL_MCP_ROOT

## Goal

`curl-mcp`'s `curl` tool accepts the null device as an `-o` target under `CURL_MCP_ROOT`, so an agent can throw a body away with `-s -o /dev/null -w "%{http_code}" URL` (or `-o NUL` on Windows), as with upstream curl.

## Context

From the bug report `2026-11-18T1233-Curl-bug-report.md` (finding 1, Medium), found while writing the *Surly* cookbook. Build tested: commit `b140f6d88c`, Windows 11; `curl-mcp` serverInfo `Curl.Mcp 0.1.0.0`. Check it still reproduces on the current branch first.

**Repro.** Start curl-mcp with `CURL_MCP_ROOT` set to an existing folder, then call the `curl` tool against any HTTP server:

```json
{"name": "curl", "arguments": {"args": ["-s", "-o", "NUL", "-w", "%{http_code}", "http://127.0.0.1:<port>/hello.txt"]}}
{"name": "curl", "arguments": {"args": ["-s", "-o", "/dev/null", "-w", "%{http_code}", "http://127.0.0.1:<port>/hello.txt"]}}
```

**Observed:** both are tool errors and curl never runs (`exitCode: null`):
- `NUL`: `failure.kind: "PathRefused"`, text `Refused -o: -o 'NUL' cannot be resolved: The parameter is incorrect. : '\.\NUL'.`
- `/dev/null`: `failure.kind: "PathRefused"`, text `Refused -o: -o '/dev/null' resolves to '\dev\null', which is outside CURL_MCP_ROOT (...)`.
- `-o copy.txt` inside the root works: `exitCode: 0`, stdout `200`.

**Expected:** the null device is not a file inside or outside the root; it is the conventional way to discard a body, and upstream curl accepts both spellings. The root confinement treats `NUL` (Windows) and `/dev/null` (every platform, as curl's documentation spells it) as allowed, discard-only targets.

**Impact:** an agent wanting only a status code must download the body to stdout or into a file in the root.

Where to start: `Curl.Mcp.UnitLibrary/McpConfinementRoot.cs` (path resolution against the root) and `McpPathVerdict.cs`. Decide which spellings count (case of `NUL`, `nul:`, `\.\NUL`, `/dev/null` on Windows, which the console maps to the null device) by what `Curl.Console` itself treats as the null device for `-o`, and record the decision in an ADR (decided by Claude under Stewart's delegation). Only output options may use it; any other path option keeps today's confinement.

## Acceptance criteria

- [x] With `CURL_MCP_ROOT` set, the `curl` tool runs `-s -o /dev/null -w "%{http_code}" <url>` and returns `exitCode: 0` and stdout `200`, on Windows, Linux and macOS.
- [x] On Windows, the same call with `-o NUL` returns `exitCode: 0` and stdout `200`.
- [x] A path that only looks like the null device (e.g. `-o /dev/null/x`, `-o ../NUL.txt` outside the root) is still refused with `PathRefused`.
- [x] Focused unit tests in `Curl.Mcp.UnitTests` pin each case above (Windows-only ones marked `[OSCondition(OperatingSystems.Windows)]`).
- [x] An ADR records which null-device spellings the confinement allows, and why.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Decision (ADR-1301): `-o` alone accepts exactly `/dev/null` and `NUL` (any case); lookalikes and other options stay confined. Console maps both to the null device on Windows (measured). `touches` widened to the real place of the rule (McpOptionValuePaths.cs) and McpArgumentCheckerTests.cs; no other Doing task holds them. The NUL rows are platform-neutral, so no OSCondition. Linux/macOS criterion met by platform-neutral tests.

## Log

- 2026-11-18: Created from bug report 2026-11-18T1233-Curl-bug-report.md, finding 1.
- 2026-11-18: Backlog -> Doing. Criteria at claim: e262f720 b1ab0af4 2ca0edb9 e965303b c215a6a1 3d836d5e.
- 2026-11-18: Doing -> Done. null device accepted for -o; ADR-1301; build clean, 555 tests, quality 100/100
