---
id: BL-3503
title: Set aside a new failing-test finding whose test passes on the clean audited commit
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Audit/Tools/Write-AuditFindings.ps1]
lane: no
requirement: none
created: 2026-10-25
completed: 2026-10-25
---
# BL-3503 — Set aside a new failing-test finding whose test passes on the clean audited commit

## Goal

A new finding whose reproduction is one failing-test command (`dotnet test <project> --filter "<filter>"`) is set aside, not filed, when that test passes on the clean audited commit, so a plant's effect reported as a failing test - AF-0426, PD-303 seen as upstream cases 391 and 1241 failing - is never filed.

## Context

Follow-up to BL-3502, which reruns only a *mechanical* (mutation) reproduction on the clean audited commit before filing a new finding. AF-0426 had `reproduction: none` and the command `dotnet test Curl.Conformance.UnitTests --filter "FullyQualifiedName~UpstreamCase_RunThroughCurl_HoldsTheRatchet&TestCategory!=Integration"`, so BL-3502 alone would still file it. `Invoke-MutationTest.ps1` shows how to check out `-Commit` in a throwaway worktree beside the repository and remove it afterwards. Accept only a command that is exactly one `dotnet test` of a project in the tree with a `--filter`, never any other command an auditor wrote.

## Acceptance criteria

- [x] With `-RerunReproductions`, a new finding whose reproduction command is a single `dotnet test <project> --filter "..."` is rerun on the clean audited `-Commit` in a throwaway worktree that is removed afterwards; if the test passes there, the finding is listed in `set-aside.json` with its reason and no `AF-*.md` is written.
- [x] A finding whose test also fails on the clean commit, whose rerun cannot tell, or whose command is anything else is filed as before.
- [x] `Write-AuditFindings.ps1 -SelfTest` covers the set-aside, the filed and the other-command cases (with the rerun stubbed) and passes.
- [x] Done on the `audit` branch; its pull request is merged once CI passes on Windows, Linux and macOS.

## Notes

## Log

- 2026-10-25: Created.
- 2026-10-25: Backlog -> Doing. Criteria at claim: 13e1a945 02264073 8ba38dad 68e59f8c.
- 2026-10-25: Doing -> Backlog. Waiting on CI for audit PR #233 (commit 93e08e8d6); reclaim to tick the criteria and finish once Windows, Linux and macOS pass.
- 2026-10-25: Backlog -> Doing. Criteria at claim: 13e1a945 02264073 8ba38dad 68e59f8c.
- 2026-10-25: Doing -> Done. Write-AuditFindings.ps1 sets aside a new failing-test finding whose single dotnet test --filter passes on the clean audited commit (throwaway worktree); self-test 50 -> 55 checks pass; audit 93e08e8d6, PR #233 merged as 70a7db521 after CI green on Windows, Linux, macOS.
