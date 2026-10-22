---
id: BL-2133
title: RunAudit sets its herdr tab caption on every early exit, not only at the end
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Audit/RunAudit.ps1]
lane: no
requirement: none
created: 2026-10-13
completed: 2026-10-13
---
# BL-2133 — RunAudit sets its herdr tab caption on every early exit, not only at the end

## Goal

An audit run's herdr tab never keeps a "running" caption after its process has ended:
every way `RunAudit.ps1` can stop leaves a caption ending in "read".

## Context

Stewart saw two "Audit · seeding" tabs at once on 2026-10-13. Only one audit was running.
These tabs were dead runs whose captions never changed:
- "Audit 01:18 · seeding": run 20261010-011813 died during seeding with "audit failed: ...
  seeder.jsonl ... being used by another process".
- "Audit 21:30 · starting" and "Audit 14:52 · starting": runs that refused or exited before
  they began.

`Audit/RunAudit.ps1` sets "done, read" when a run finishes and "FAILED, read" on some
failures. Failures during seeding, and refusals at start, exit without a final caption.
Look at how it renames its tab (`herdr tab rename`, or the shared tab-caption helper) and
at every `exit` and `throw` path.

Audit path: interactive only. Do the work on a branch built from master while an audit
uses `Z:\repos\Curl.auditbranch`, and land it through a pull request to master, merged
once CI is green on all three platforms.

## Acceptance criteria

- [x] Every exit path of `RunAudit.ps1` started with `-NewTab` sets a final caption that ends in "read": "done, read" on success, "FAILED, read" on an error (a `trap` or `try`/`finally` covers thrown errors), and "REFUSED, read" when it refuses to start.
- [x] `RunAudit.ps1 -SelfTest` proves the final caption for success, a thrown error and a refusal.
- [x] The script's help says so.

- [x] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing. interactive session
- 2026-10-13: Doing -> Done. PR #134 merged: every RunAudit exit sets a caption ending in read
