---
id: BL-3508
title: Make the split-hotspot filing's final trace say fetch when every fetch attempt failed
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/CiWatch.ps1]
requirement: none
created: 2026-10-25
completed:
---
# BL-3508 — Make the split-hotspot filing's final trace say fetch when every fetch attempt failed

## Goal

When every attempt of `Invoke-SplitHotspotFiling` fails at `git fetch`, its final trace names the fetch, not the push, and its header comment describes both reasons it retries.

## Context

Found by the shift-end review (ADR-0710) of `ee9fe883e...19cfdb2b6`, by the code-reviewer and silent-failure-hunter reviewers.

BL-3506 changed `Invoke-SplitHotspotFiling` in `DarkFactory/CiWatch.ps1` so that a failed `git fetch` (line 723) does `continue` to the next of its three attempts. When all three fail, the loop falls through to line 757, which traces "could not push the split task to $Branch; trying again next step". That line prints on every step, yet no push was attempted. The `split-fetch` line itself is written only once per outage, so the false line is the one that repeats.

The function's header comment ("up to three tries when the push races a lane") also does not mention the fetch retry. CLAUDE.md requires that code say what it does and do what it says.

## Acceptance criteria

- [ ] When no attempt got past `git fetch`, the final trace of `Invoke-SplitHotspotFiling` says the fetch failed. When a push failed, it still says the push failed.
- [ ] The function's header comment names both reasons it retries: a failed fetch, and a push that races a lane.
- [ ] `RunDarkFactory.ps1 -TestCiWatch` passes.

## Notes

## Log

- 2026-10-25: Created.
