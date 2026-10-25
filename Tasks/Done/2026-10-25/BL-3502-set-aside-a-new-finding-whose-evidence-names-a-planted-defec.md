---
id: BL-3502
title: Set aside a new finding whose evidence names a planted defect's behaviour from another file
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
# BL-3502 — Set aside a new finding whose evidence names a planted defect's behaviour from another file

## Goal

A new finding whose evidence describes a planted defect's effect, though its location is another file (for example a failing upstream case the plant causes), is set aside rather than filed.

## Context

Audit 2026-10-24_2106 (run `Z:\repos\Curl.audit\20261024-210628`) filed AF-0426: upstream cases 391 and 1241 fail because `--path-as-is` sends no request. That is planted defect PD-303 (the `--path-as-is` row removed from `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs`), reported by the quality auditor at `Curl.Conformance.UnitTests/UpstreamConformanceTests.cs:129`. BL-3465's set-aside matches only a finding located in the planted file, so this one was filed and Stewart rejected it. A cheap, sound test: run the finding's mechanical reproduction on the clean audited commit (the runner already does this for re-audits); if it does not reproduce there, the finding came from the planted tree.

## Acceptance criteria

- [x] A new finding whose mechanical reproduction does not reproduce on the clean audited commit is set aside with the reason in `set-aside.json`, and no `AF-*.md` is written for it.
- [x] A new finding that reproduces on the clean commit, or that has no mechanical reproduction, is filed as before.
- [x] The script's self-test covers both cases and passes.
- [x] Done on the `audit` branch; its pull request is merged once CI passes on Windows, Linux and macOS.

## Notes

## Log

- 2026-10-25: Created.
- 2026-10-25: Backlog -> Doing. Criteria at claim: 953f1029 ad265e9d 7d9b3c97 68e59f8c.
- 2026-10-25: Doing -> Backlog. Change pushed to audit as a1f2710d5 (self-test 50/50); waits for PR #230 to merge on green CI, then reclaimed to finish. Failing-test findings like AF-0426 itself filed as BL-3503
- 2026-10-25: Backlog -> Doing. Criteria at claim: 953f1029 ad265e9d 7d9b3c97 68e59f8c.
- 2026-10-25: Doing -> Done. Write-AuditFindings.ps1 sets aside a new surviving mutant the clean audited commit kills, with its reason in set-aside.json, and files the rest as before; self-test passes (a1f2710d5; PR #230 merged as d5489de45 after CI passed on Windows, Linux and macOS)
