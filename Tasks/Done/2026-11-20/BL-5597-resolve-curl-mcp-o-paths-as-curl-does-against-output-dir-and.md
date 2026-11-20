---
id: BL-5597
title: Resolve curl-mcp -o paths as curl does: against --output-dir, and with #1 glob references
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Mcp.UnitLibrary/McpOptionValuePaths.cs, Curl.Mcp.UnitLibrary/McpPathCheck.cs, Curl.Mcp.UnitLibrary/McpArgumentChecker.cs, Curl.Mcp.UnitTests/McpArgumentCheckerTests.cs]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-5597 — Resolve curl-mcp -o paths as curl does: against --output-dir, and with #1 glob references

## Goal

The `curl` tool's root confinement judges an `-o` path where curl will actually write it, so commands that write inside `CURL_MCP_ROOT` run and only paths that would leave the root are refused.

## Context

From Surly cookbook bug report `2026-11-19T1942-Curl-bug-report.md` (batch 1 of the 1,000-recipe plan), finding #4; Surly cookbook bug report `2026-11-19T2203-Curl-bug-report.md` (batch 2), finding #6. Build tested by the reports: the Curl port at `b9ca725077` (`curl`, `curl-mcp 0.1.0`, `Curl.Api 0.2.0`, `Libcurl 0.2.0`), Windows 11 (Linux and macOS through the cookbook's CI where stated); server Surl `763754785`. Check that it still reproduces on the current branch first.

Two refusals of commands that write inside the root:
- `--output-dir downloads -o ../guide.txt` is refused `PathRefused`: `-o` is checked against the root and `--output-dir` ignored. Real curl writes `downloads/../guide.txt`, inside the root (1942 #4).
- `-o "reports/report-#1.csv"` with a URL glob is refused before curl runs; `-O --output-dir reports` works (2203 #6). The `#1` reference should be judged on the literal directory part (or each expansion), not refused as unknown.
Combine `--output-dir` with `-o` as curl does (including `--create-dirs`), and treat `#N` glob references as file-name characters for the directory check.

## Acceptance criteria

- [x] `--output-dir downloads -o ../guide.txt` inside the root runs and writes `guide.txt` in the root; `--output-dir downloads -o ../../x` that leaves the root is refused.
- [x] `-o "reports/report-#1.csv"` with a URL glob runs and writes inside `reports/`; `-o "../x-#1"` that leaves the root is refused.
- [x] Focused unit tests in `Curl.Mcp.UnitTests` pin each case; `dotnet build` is clean and the fast tests pass.

## Notes

- touches widened to McpArgumentChecker.cs (where the checker threads --output-dir) and its tests; no other Doing task named them. The checker pre-scans the arguments for the last --output-dir (curl applies it to every -o wherever it stands) and joins it before the -o name; #N becomes a file-name character (the literal directory part is judged). --output-dir set only inside a -K file is not joined: that stays the stricter root-relative check. Measured: Curl.Mcp.UnitLibrary 100% line and branch, worst CRAP 10.

## Log

- 2026-11-20: Created from Surly bug reports (1942 #4, 2203 #6).
- 2026-11-20: Backlog -> Doing. Criteria at claim: 0a81666f a5bbb262 c5fa87fb.
- 2026-11-20: Doing -> Done. curl-mcp -o is judged below --output-dir and with #N as file-name characters
