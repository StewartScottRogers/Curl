---
id: BL-5684
title: End curl-mcp curl tool stderr lines in CR LF, not CR CR LF, on Windows
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Mcp.UnitLibrary/McpCurlToolResult.cs, Curl.Mcp.UnitLibrary/ICurlCommandRunner.cs, Curl.Mcp.UnitTests/McpCurlToolTests.cs]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-5684 — End curl-mcp curl tool stderr lines in CR LF, not CR CR LF, on Windows

## Goal

The `curl-mcp` `curl` tool's `standardError` holds exactly the bytes `curl.exe` writes to stderr, so `-v` lines end in CR LF on Windows (and LF elsewhere), never CR CR LF.

## Context

From Surly cookbook bug report `2026-11-21T0547-Curl-bug-report.md` (batch 9 of the 1,000-recipe plan), finding 5. Build tested: Curl `7d167fa10a`, Windows 11; server Surl `9ad60d80c`. Check it still reproduces on the current branch first.

With `-v`, the `curl` tool's `standardError.content` lines end in `\r\r\n` on Windows. Something translates `\n` to `\r\n` once more on text that already holds `\r\n` (a TextWriter with `NewLine`, or a stream read as lines and joined with `Environment.NewLine`). Capture the stderr bytes as written, as stdout is, and check stdout for the same problem.

## Acceptance criteria

- [x] A `curl` tool call with `-v` returns `standardError` whose bytes equal what `curl.exe -v` writes for the same request, pinned by a test that runs on Windows and one that runs elsewhere.
- [x] stdout through the tool is byte-identical to `curl.exe`'s for a body holding CR LF and lone LF, pinned by a test.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Not a bug. Measured 2026-11-21: Windows System32 curl.exe -v writes server header lines as `< ...
` (its text-mode stderr turns the server's CR LF into CR CR LF); Curl.Console curl.exe writes the identical header bytes, and stdout `a
b
c` is byte-identical. The tool passes the runner's bytes through unchanged, so it already matches curl.exe, the drop-in goal; the report's "never CR CR LF" contradicts its own "equal what curl.exe writes". No production change. Pinned by two tests in McpCurlToolTests (platform-neutral: bytes in equal bytes out), so the "runs elsewhere" part is the same test. Added the test file to touches.

## Log

- 2026-11-21: Created from Surly bug report 2026-11-21T0547, finding 5.
- 2026-11-21: Backlog -> Doing. Criteria at claim: 4a980250 38306fae 3d836d5e.
- 2026-11-21: Doing -> Done. Not a bug: real curl.exe also writes CR CR LF; pinned by tests
