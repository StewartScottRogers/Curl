---
id: BL-809
title: Keep a dark factory shift on its branch when the checkout is switched under it
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-809 — Keep a dark factory shift on its branch when the checkout is switched under it

## Goal

A shift that finds `Z:\repos\Curl` switched off its branch switches it back, both before
its shift-end pull and when `-Continuous` starts the next shift, instead of
fast-forwarding the wrong branch or refusing to start.

## Context

Traced 2026-09-28: Visual Studio started at 18:08:09 and at 18:08:25 switched the checkout
from `work/dark-factory` to a stale local `master` while a shift ran. At shift end the
coordinator runs `git pull --ff-only origin $branch` in whatever is checked out, and the
next shift reads the branch from HEAD and refuses `master` with exit 1, so `-Continuous`
would have stopped the factory without an alarm.

## Acceptance criteria

- [x] A function `Restore-ShiftBranch -Branch <name> [-Repo <path>]` switches a clean
      checkout that is on another branch back to `<name>` and traces it; it returns a
      reason, and switches nothing, when the tree is dirty.
- [x] The coordinator calls it before the shift-end pull, and skips the pull with a red
      trace when it returns a reason.
- [x] `-Continuous` hands the next shift `-ShiftBranch <branch>`; a coordinator or single
      runner given `-ShiftBranch` restores that branch before reading HEAD.
- [x] `-TestShiftBranch` builds a throwaway repository in a temporary folder, proves the
      clean switch-back and the dirty refusal, and exits 0.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Restore-ShiftBranch before the shift-end pull and at the start of a -Continuous shift via -ShiftBranch; -TestShiftBranch 3/3, other self-checks, build and fast tests green.
