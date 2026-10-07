---
id: BL-1029
title: Make the audit-guard check required on master, so deleting the job from the factory branch's ci.yml cannot switch it off
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-998]
touches: [.github]
lane: no
requirement: none
created: 2026-09-29
completed: 2026-09-30
---
# BL-1029 — Make the audit-guard check required on master, so deleting the job from the factory branch's ci.yml cannot switch it off

## Goal

A pull request from `work/dark-factory` cannot merge into `master` unless the `Audit paths untouched by the dark factory` check ran and passed on it.

## Context

Found while delivering BL-998 (2026-09-29). GitHub runs the workflow file from the branch being built, so the `audit-guard` job exists on `work/dark-factory` only while that branch's own `ci.yml` defines it. `ci.yml` is a guarded path, but the only thing that detects a change to it is that same job: a lane that deletes the job from `ci.yml` removes the check that would have caught it, and `Invoke-MergeToMaster` only asks whether the CI run as a whole succeeded.

The robust fix is a GitHub branch protection rule (or ruleset) on `master` that requires the status check `Audit paths untouched by the dark factory` for pull requests from `work/dark-factory`. A missing check then blocks the merge instead of passing it. This is a repository settings change, so it is Stewart's to make or approve (Claude can apply it with `gh api` once he says yes).

## Acceptance criteria

- [x] `master` has a branch protection rule or ruleset requiring the `Audit paths untouched by the dark factory` status check.
- [x] The dark factory's shift-end pull request still merges when that check passes (the rule does not also require checks the `audit` pull requests never run).
- [x] ADR-0267 records the rule.

## Notes

- 2026-09-30, Stewart: "make the check required". Created repository ruleset 24235022 "Audit guard required on master": target refs/heads/master, active, required status check `Audit paths untouched by the dark factory` (github-actions, integration 15368), no bypass actors (the factory merges with Stewart's credentials, so an admin bypass would let `gh pr merge --admin` skip it). No workflow or script pushes to master directly; RunDarkFactory.ps1 merges with `gh pr merge --merge`, no `--admin`.
- Probe: pull request #26 from work/dark-factory showed BLOCKED while the check was pending and UNSTABLE (mergeable; only optional checks outstanding) once it passed; closed without merging.
- Recorded as ADR-0267 section 7.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. master's ruleset requires the audit guard check with no bypass, so deleting the job from the factory branch blocks its merge.
