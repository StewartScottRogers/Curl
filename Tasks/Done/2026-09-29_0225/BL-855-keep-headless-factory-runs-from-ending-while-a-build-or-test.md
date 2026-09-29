---
id: BL-855
title: Keep headless factory runs from ending while a build or test waits in the background
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-855 — Keep headless factory runs from ending while a build or test waits in the background

## Goal

A headless dark factory run finishes its build and fast tests in the foreground and never
ends its reply to wait for a background notification, so it no longer ends in Doing with
exit 0 and its work stashed.

## Context

BL-674 (2026-09-28 12:09) and BL-823 (2026-09-28 21:21, lane 4) both ended "run ended in
Doing, exit 0". Their last words were "Build, tests and coverage re-run are still going
... Waiting for it." and "Build and fast tests are running; I'll be notified when they
finish." A tool call that runs past its timeout (at most 10 minutes by default) is moved
to the background. The run then ends its reply to wait, and in `claude -p` the process
exits with it. With six lanes building at once, the fast tests alone can pass 10 minutes.

## Acceptance criteria

- [x] `Invoke-TaskRun` starts `claude` with `BASH_DEFAULT_TIMEOUT_MS=1800000` and
      `BASH_MAX_TIMEOUT_MS=3600000` in its environment.
- [x] `$Prompt`, `$LanePrompt` and `$ResolvePrompt` each carry a rule: the run ends when
      the reply ends, and anything in the background dies with it. So build and test in
      the foreground with the Bash tool and a timeout of up to 3600000 ms, and never end
      the reply to wait for a notification.
- [x] The script parses and `-TestAutoLanes`, `-TestMachineProbe` and `-TestShiftBranch`
      pass.

## Notes

Takes effect for runs started by the next shift; the running coordinator keeps its copy.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Runs start with BASH_DEFAULT/MAX_TIMEOUT_MS 30/60 min and all three headless prompts forbid ending the reply to wait; self-checks, build and fast tests (31 assemblies) green.
