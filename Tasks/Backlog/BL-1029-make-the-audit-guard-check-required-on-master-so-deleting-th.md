---
id: BL-1029
title: Make the audit-guard check required on master, so deleting the job from the factory branch's ci.yml cannot switch it off
priority: High
assignee: Stewart
pipeline: direct
depends-on: [BL-998]
touches: [.github]
lane: no
requirement: none
created: 2026-09-29
completed:
---
# BL-1029 — Make the audit-guard check required on master, so deleting the job from the factory branch's ci.yml cannot switch it off

## Goal

A pull request from `work/dark-factory` cannot merge into `master` unless the `Audit paths untouched by the dark factory` check ran and passed on it.

## Context

Found while delivering BL-998 (2026-09-29). GitHub runs the workflow file from the branch being built, so the `audit-guard` job exists on `work/dark-factory` only while that branch's own `ci.yml` defines it. `ci.yml` is a guarded path, but the only thing that detects a change to it is that same job: a lane that deletes the job from `ci.yml` removes the check that would have caught it, and `Invoke-MergeToMaster` only asks whether the CI run as a whole succeeded.

The robust fix is a GitHub branch protection rule (or ruleset) on `master` that requires the status check `Audit paths untouched by the dark factory` for pull requests from `work/dark-factory`. A missing check then blocks the merge instead of passing it. This is a repository settings change, so it is Stewart's to make or approve (Claude can apply it with `gh api` once he says yes).

## Acceptance criteria

- [ ] `master` has a branch protection rule or ruleset requiring the `Audit paths untouched by the dark factory` status check.
- [ ] The dark factory's shift-end pull request still merges when that check passes (the rule does not also require checks the `audit` pull requests never run).
- [ ] ADR-0267 records the rule.

## Notes

## Log

- 2026-09-29: Created.
