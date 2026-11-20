---
id: BL-5629
title: MCP curl tool refuses an -o name with a #N glob reference again unless every URL glob alternative stays inside CURL_MCP_ROOT
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Mcp.UnitLibrary/McpOptionValuePaths.cs, Curl.Mcp.UnitLibrary/McpArgumentChecker.cs, Curl.Mcp.UnitTests/McpArgumentCheckerTests.cs]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-5629 — MCP curl tool refuses an -o name with a #N glob reference again unless every URL glob alternative stays inside CURL_MCP_ROOT

## Goal

The MCP `curl` tool never lets an `-o` name holding a `#<n>` glob reference write outside `CURL_MCP_ROOT`.

## Context

Found by the shift-end review of 2026-11-20 (code-reviewer, type-design-analyzer and silent-failure-hunter all reported it) in the diff b73256235592..071c3980c0.

`Curl.Mcp.UnitLibrary/McpOptionValuePaths.cs`, `WrittenFilePaths` and `WithoutGlobReferences` (about lines 260-290). Before this shift an `-o` value holding `#<digit>` was refused, because curl fills it from the URL glob after the check. Now each `#<n>` is replaced by `_` and the literal name is judged. curl fills `#1` with the raw text of a `{...}` alternative, which may hold `/` and `..`. The checker only looks at URL glob characters for `file://` URLs and `-T`.

Failure: `["-o", "#1", "http://h/{../../../home/u/.bashrc,x}"]` is checked as `_`, which is inside the root, so it is allowed. curl then writes `../../../home/u/.bashrc` outside the root, off Windows. The new rows in `Curl.Mcp.UnitTests/McpArgumentCheckerTests.cs` (about lines 61-67) use only harmless alternatives such as `{a,b}`.

## Acceptance criteria

- [x] An `-o` name with `#<n>` is refused unless every `{}` and `[]` alternative of every URL argument makes a name that stays inside `CURL_MCP_ROOT`. Checking means expanding the URL globs, or refusing any alternative holding `/`, `\` or `..`.
- [x] `McpArgumentCheckerTests` has a row where a `{..}` alternative escapes the root, `-o "#1" "http://h/{../../x,y}"`, and is refused. It also has a row where `--output-dir` plus `#1` escapes, and is refused.
- [x] The existing harmless `{a,b}` rows are still allowed.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Added McpArgumentChecker.cs to touches: the check needs every argument, which only the checker sees; no other Doing task touches it.
- Decision: with an -o name holding #<n>, any argument holding a {} or [] alternative with /,  or .. refuses the -o (over-cautious for non-URL arguments such as -d JSON, chosen as the simplest safe rule). Quality run: 0 failing members.

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing. Criteria at claim: 28e141b7 666c5cea bbaf80af 3d836d5e.
- 2026-11-20: Doing -> Done. Refuses -o #<n> when a URL glob alternative escapes the root
