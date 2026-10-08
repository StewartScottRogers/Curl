---
id: BL-1719
title: Create the Gap shared project with a folder-map README and list it in Curl.slnx
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Gap/Gap.shproj, Gap/Gap.projitems, Gap/README.md, Curl.slnx]
model: sonnet
requirement: none
created: 2026-10-08
completed:
---
# BL-1719 — Create the Gap shared project with a folder-map README and list it in Curl.slnx

## Goal

`Gap/` exists as a shared project like `Audit/`, listed in `Curl.slnx`, with a `README.md` that maps the folders the gap analysis office will hold.

## Context

The gap analysis office is designed in ADR-0433
(`Documentation/Planning/Decisions/ADR-0433-a-gap-analysis-office-measures-curl-against-upstream-curl-releases.md`).
It measures how far Curl is from upstream curl's own release data and narrows that distance
over time. This task lays down only the container. Every other gap task (BL-1720 to BL-1748)
fills it.

Copy the pattern of `Audit/`:

- `Audit/Audit.shproj` and `Audit/Audit.projitems`: a shared project with a recursive
  wildcard glob that takes every file. The comment in `Audit.projitems` explains why the glob
  is load-bearing. Copy it, with the office's name changed.
- `Curl.slnx` line 125 lists `<Project Path="Audit/Audit.shproj" />` in the shared-projects
  run. Add `<Project Path="Gap/Gap.shproj" />` directly after it, and extend the comment near
  the top of `Curl.slnx` that names the shared projects (line 8: "Documentation, Tasks,
  .claude/Claude and Audit are shared projects") so it names `Gap` too.

Give `Gap.shproj` a new GUID (`[guid]::NewGuid()`), and use the same value for `SharedGUID`
in `Gap.projitems`. Visual Studio pairs the two files by that value. Set
`Import_RootNamespace` to `Gap`.

`Gap/README.md` starts with one paragraph saying what the office is (it measures Curl
against upstream curl's release data area by area, ADR-0433). Then comes a folder-map table
in the shape of `Audit/README.md`'s, with columns Path, What it holds, Built by. List the
rows below. Every row is planned, so its "Built by" cell reads `planned, BL-####`:

| Path | Built by |
| --- | --- |
| `Instructions/` (formats, analyst rules, one method per area) | BL-1720, BL-1737 to BL-1739 |
| `Baselines/` (targeted version, pinned tarball hashes) | BL-1721 |
| `Upstream/<version>/` (upstream inventories per area) | BL-1723 to BL-1727 |
| `Findings/` (`GF-####-*.md`) | BL-1720, BL-1731 |
| `Scorecards/` (one per run, plus `history.json`) | BL-1720, BL-1732 |
| `Tools/` | BL-1721 to BL-1735 |
| `Triage.md` | BL-1734 |
| `RunGapAnalysis.cmd`, `RunGapAnalysis.ps1` | BL-1740, BL-1741 |
| `../.claude/agents/gap-*.md` | BL-1737 to BL-1739 |

BL-1745 rewrites the README once the parts exist. This one states only what is true now: the
folder holds the project files and this map.

## Acceptance criteria

- [ ] `Gap/Gap.shproj` and `Gap/Gap.projitems` exist. The `ProjectGuid` in the first equals the `SharedGUID` in the second, the glob is recursive, and both project files are excluded from it, as in `Audit/Audit.projitems`.
- [ ] `Curl.slnx` lists `<Project Path="Gap/Gap.shproj" />` directly after `Audit/Audit.shproj`, with no solution folder around it, and its header comment names `Gap` among the shared projects.
- [ ] `Gap/README.md` holds the opening paragraph citing ADR-0433 and the folder-map table above, with every row marked planned and its task ID.
- [ ] `dotnet build Curl.slnx` succeeds with no new warnings, and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-08: Created.
