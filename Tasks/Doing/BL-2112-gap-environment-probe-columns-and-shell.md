---
id: BL-2112
title: Gap environment: probe COLUMNS and SHELL
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Gap/Tools]
lane: no
requirement: none
created: 2026-10-12
completed:
---
# BL-2112 — Gap environment: probe COLUMNS and SHELL

## Goal

COLUMNS and SHELL are measured, or excluded with a measured reason.

## Context

Gap run 2026-10-11_1950 scored environment 24/42 (57.1%): every measured item matches, and 18 are unmeasured with reason no-probe because `Gap/Tools/Measure-EnvironmentGap.ps1` has no recipe for them. Gap paths are interactive only; the work lands through a pull request from a branch built from master, merged once CI is green on Windows, Linux and macOS. Each new recipe runs through the reference curl and Curl.Console as the existing ones do (`Record-CurlExchange.ps1` for the loopback server), and the script's -SelfTest covers it. A recipe that finds a real difference is a gap, filed as a lane task by the next gap run, not hidden.

## Acceptance criteria

- [ ] `COLUMNS` has a recipe comparing the `-#` progress bar width under a set `COLUMNS`.
- [ ] `SHELL` has a recipe for whatever curl 8.21.0 uses it for (found in its source or docs and recorded in Notes), or an exclusion with that reason.
- [ ] `Measure-EnvironmentGap.ps1 -SelfTest` passes, and a local run measures neither as no-probe.

## Notes

## Log

- 2026-10-12: Created.
- 2026-10-12: Backlog -> Doing. interactive: gap environment probes
