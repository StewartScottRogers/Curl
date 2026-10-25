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
completed:
---
# BL-3502 — Set aside a new finding whose evidence names a planted defect's behaviour from another file

## Goal

A new finding whose evidence describes a planted defect's effect, though its location is another file (for example a failing upstream case the plant causes), is set aside rather than filed.

## Context

Audit 2026-10-24_2106 (run `Z:\repos\Curl.audit\20261024-210628`) filed AF-0426: upstream cases 391 and 1241 fail because `--path-as-is` sends no request. That is planted defect PD-303 (the `--path-as-is` row removed from `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs`), reported by the quality auditor at `Curl.Conformance.UnitTests/UpstreamConformanceTests.cs:129`. BL-3465's set-aside matches only a finding located in the planted file, so this one was filed and Stewart rejected it. A cheap, sound test: run the finding's mechanical reproduction on the clean audited commit (the runner already does this for re-audits); if it does not reproduce there, the finding came from the planted tree.

## Acceptance criteria

- [ ] A new finding whose mechanical reproduction does not reproduce on the clean audited commit is set aside with the reason in `set-aside.json`, and no `AF-*.md` is written for it.
- [ ] A new finding that reproduces on the clean commit, or that has no mechanical reproduction, is filed as before.
- [ ] The script's self-test covers both cases and passes.
- [ ] Done on the `audit` branch; its pull request is merged once CI passes on Windows, Linux and macOS.

## Notes

## Log

- 2026-10-25: Created.
