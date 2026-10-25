---
id: BL-3506
title: Give the split-hotspot filing's fetch failure its own trace key and a true message
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/CiWatch.ps1]
requirement: none
created: 2026-10-25
completed:
---
# BL-3506 — Give the split-hotspot filing's fetch failure its own trace key and a true message

## Goal

When `git fetch` fails in `Invoke-SplitHotspotFiling`, the trace says what really happened, under a key of its own, and the filing does not carry on from a stale branch.

## Context

Found by the shift-end review of d5489de4..287e5bf6 (ADR-0710): the code-reviewer and silent-failure-hunter both reported it.

`DarkFactory/CiWatch.ps1`, `Invoke-SplitHotspotFiling` (around lines 719-720). The fetch-failure check was copied there from `Invoke-CiWatch`, and three things are wrong with it:
- **The message is false.** It says "a run is waiting but cannot be compared with the running one", but this function files split-hotspot tasks and involves no CI run. That breaks CLAUDE.md's "say what it does" rule.
- **The key is shared.** It uses the same `'fetch'` key in `$script:CiWatch.Logged` as `Invoke-CiWatch`, so each function's `Logged.Remove('fetch')` clears the other's de-duplication.
- **The filing carries on.** After the failure it keeps going on a stale `origin/$Branch`.

The two lines are also indented one level less than the `foreach` body they sit in.

## Acceptance criteria

- [ ] The fetch check in `Invoke-SplitHotspotFiling` uses a key that no other caller uses, and its message names the split-hotspot filing, not a waiting CI run.
- [ ] A failed fetch there goes on to the next attempt (`continue`) and does not file from the stale branch.
- [ ] The two lines are indented to match the `foreach` body.
- [ ] `powershell -NoProfile -File RunDarkFactory.ps1 -TestCiWatch` passes.

## Notes

## Log

- 2026-10-25: Created.
- 2026-10-25: Backlog -> Doing. Criteria at claim: 56350b18 0bfe3444 7fa28561 cfd9f717.
