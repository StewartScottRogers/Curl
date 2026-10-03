---
id: BL-1282
title: Let the release workflow finish green when GitHub Actions may not open the audit pull request
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [.github/workflows/release-approved-findings.yml]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1282 — Let the release workflow finish green when GitHub Actions may not open the audit pull request

## Goal

A release run whose only failure is GitHub refusing to let Actions open the audit pull request ends green with a warning, since the tasks and findings are already pushed.

## Context

- 2026-10-02: run 37097944249 (Stewart's first release of AF-0005 to AF-0025) pushed BL-1261 to BL-1281 to `work/dark-factory` and the findings to `audit`, then failed: "GitHub Actions is not permitted to create or approve pull requests". The interactive session opened PR #52 instead.
- Allowing Actions to create pull requests is a repository setting, Stewart's to change; this task does not change it.

## Acceptance criteria

- [x] A failed `gh pr create` writes a line to the job summary and a `::warning::`, and no longer throws.
- [x] `dotnet build` is clean and the fast tests are green.

## Notes

- 2026-10-02: Checked by reading the step: the pushes come before the pull request, so nothing after it depends on it.
## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. A refused audit pull request is a warning, not a failed release
