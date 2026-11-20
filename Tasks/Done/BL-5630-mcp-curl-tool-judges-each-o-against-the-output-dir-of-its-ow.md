---
id: BL-5630
title: MCP curl tool judges each -o against the --output-dir of its own --next group
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Mcp.UnitLibrary/McpArgumentChecker.cs, Curl.Mcp.UnitTests/McpArgumentCheckerTests.cs]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-5630 — MCP curl tool judges each -o against the --output-dir of its own --next group

## Goal

The MCP `curl` tool joins each `-o` name only to the `--output-dir` given in its own `--next` option group, as curl does.

## Context

Found by the shift-end review of 2026-11-20 (type-design-analyzer) in the diff b73256235592..071c3980c0.

`Curl.Mcp.UnitLibrary/McpArgumentChecker.cs`, `OutputDirectoryIn` (about lines 72-95), takes the last `--output-dir` anywhere in the argument array and joins it to every `-o`. In curl, `output_dir` belongs to one operation's config, and `--next` starts a fresh one without it.

Failures:
- `--output-dir a/b/c -o x URL1 --next -o ../../y URL2`: the second `-o` is judged as `a/b/c/../../y`, which is inside the root, so it is allowed. curl writes `<root>/../../y`, outside the root.
- `-o x URL1 --output-dir .. --next --output-dir ok -o y URL2`: the first `-o` is judged under `ok`, but curl writes it under `..`.

## Acceptance criteria

- [x] The output directory is worked out per `--next` group while the arguments are walked, and an `-o` in a group with no `--output-dir` is judged on its own.
- [x] `McpArgumentCheckerTests` has a row for each failure above, and each is refused.
- [x] Existing `--output-dir` rows still pass.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

The checker now walks the arguments twice: a recording walk (judges no path) notes each --next group's --output-dir, then the judging walk joins each -o only to its own group's directory. A -K file inherits the directory of the group that names it. Group boundaries are counted where the real walk consumes --next (also bundled -:), so a "--next" taken as an option value cannot misalign them. Rows added to McpArgumentCheckerTests for both failures plus an allowed one. Measure-CodeQuality for Curl.Mcp.UnitLibrary: 0 failing members.

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing. Criteria at claim: b109abfd 1365623a f8e567c5 3d836d5e.
- 2026-11-20: Doing -> Done. Per-group --output-dir implemented, tests green, quality clean
