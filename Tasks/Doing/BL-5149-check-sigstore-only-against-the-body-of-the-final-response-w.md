---
id: BL-5149
title: Check --sigstore only against the body of the final response when -L follows redirects
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Sigstore.UnitLibrary/SigstoreTransferHook.cs, Curl.Extension.Sigstore.UnitTests]
requirement: none
created: 2026-11-13
completed:
---
# BL-5149 — Check --sigstore only against the body of the final response when -L follows redirects

## Goal

<!-- One sentence: the observable outcome once this task is done. -->

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [x] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

- Task body was an empty template; mirrored BL-5122: SigstoreTransferHook now skips judging (and drops the held body of) a hop the follower will follow. Six new tests cover followed, tampered final, unfollowed, limit reached, no limit, no report. Platform-neutral, no sockets.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: fbecace0.
