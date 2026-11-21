---
id: BL-5684
title: End curl-mcp curl tool stderr lines in CR LF, not CR CR LF, on Windows
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Mcp.UnitLibrary/McpCurlToolResult.cs, Curl.Mcp.UnitLibrary/ICurlCommandRunner.cs]
requirement: none
created: 2026-11-21
completed:
---
# BL-5684 — End curl-mcp curl tool stderr lines in CR LF, not CR CR LF, on Windows

## Goal

The `curl-mcp` `curl` tool's `standardError` holds exactly the bytes `curl.exe` writes to stderr, so `-v` lines end in CR LF on Windows (and LF elsewhere), never CR CR LF.

## Context

From Surly cookbook bug report `2026-11-21T0547-Curl-bug-report.md` (batch 9 of the 1,000-recipe plan), finding 5. Build tested: Curl `7d167fa10a`, Windows 11; server Surl `9ad60d80c`. Check it still reproduces on the current branch first.

With `-v`, the `curl` tool's `standardError.content` lines end in `\r\r\n` on Windows. Something translates `\n` to `\r\n` once more on text that already holds `\r\n` (a TextWriter with `NewLine`, or a stream read as lines and joined with `Environment.NewLine`). Capture the stderr bytes as written, as stdout is, and check stdout for the same problem.

## Acceptance criteria

- [ ] A `curl` tool call with `-v` returns `standardError` whose bytes equal what `curl.exe -v` writes for the same request, pinned by a test that runs on Windows and one that runs elsewhere.
- [ ] stdout through the tool is byte-identical to `curl.exe`'s for a body holding CR LF and lone LF, pinned by a test.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-21: Created from Surly bug report 2026-11-21T0547, finding 5.
- 2026-11-21: Backlog -> Doing. Criteria at claim: 4a980250 38306fae 3d836d5e.
