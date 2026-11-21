---
id: BL-5646
title: Let the curl-mcp curl tool accept -T with a {a,b} or [1-2] glob, confined to CURL_MCP_ROOT
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Mcp.UnitLibrary/McpOptionValuePaths.cs, Curl.Mcp.UnitLibrary/McpPathCheck.cs]
requirement: none
created: 2026-11-20
completed:
---
# BL-5646 — Let the curl-mcp curl tool accept -T with a {a,b} or [1-2] glob, confined to CURL_MCP_ROOT

## Goal

The `curl` tool runs `-T "{a,b}.txt"` and `-T "file[1-2].txt"` as curl does, checking each expanded file against `CURL_MCP_ROOT`.

## Context

From Surly cookbook bug report `2026-11-20T1836-Curl-bug-report.md` (batch 6 of the 1,000-recipe plan, and batch 5's CI on Linux and macOS), finding 7. Build tested: Curl `7d167fa10a` (`curl`, `curl-mcp`, `Curl.Api`), Windows 11 (Linux and macOS through the cookbook's CI where stated); server Surl `9ad60d80c`. Check it still reproduces on the current branch first.

`-T` with a `{a,b}` or `[1-2]` glob is refused before curl runs; curl accepts it and uploads each file. BL-5597 (Done) taught the checker `#N` references in `-o`; `-T` globs need the same care: expand the glob as curl does (or check its literal directory part and every expansion) and refuse only when an expanded path leaves the root.

## Acceptance criteria

- [ ] `-T "{a,b}.txt"` and `-T "f[1-2].txt"` naming files inside the root run and upload each file.
- [ ] A `-T` glob any of whose expansions leaves the root (for example `"{a,../x}.txt"`) is refused with `PathRefused`.
- [ ] Focused unit tests in `Curl.Mcp.UnitTests` pin each case; `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-20: Created from Surly bug report 2026-11-20T1836, finding 7.
- 2026-11-20: Backlog -> Doing. Criteria at claim: 2fc017ac 72f3fa2a c5fa87fb.
