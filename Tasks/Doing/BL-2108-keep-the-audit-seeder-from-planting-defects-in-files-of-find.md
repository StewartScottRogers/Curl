---
id: BL-2108
title: Keep the audit seeder from planting defects in files of findings awaiting a re-audit
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Audit/Tools, .claude/agents]
lane: no
requirement: none
created: 2026-10-12
completed:
---
# BL-2108 — Keep the audit seeder from planting defects in files of findings awaiting a re-audit

## Goal

An accepted finding's re-audit verdict is never set aside for overlapping a planted defect,
because the seeder plants no defect in a file that a finding waiting on a re-audit names.

## Context

AF-0064 and AF-0065 stayed open for 8 audits. On audits 2026-10-11_0051 and
2026-10-11_1638 their auditor found each no longer reproducing, but both verdicts were set
aside because planted defect PD-201 sat in the same file, `Curl.Console/CurlCommandRunner.cs`
(ADR-0422's overlap rule). Stewart closed them by hand on 2026-10-12 (PR #122). Nothing stops
the next audit from doing the same to another finding.

Start at `.claude/agents/audit-seeder.md`, `Audit/RunAudit.ps1`'s seeding step and the overlap
check that sets verdicts aside, and `Audit/PlantedDefects/`. Audit paths: interactive only,
done on the `audit` branch.

## Acceptance criteria

- [ ] Before seeding, the audit run passes the seeder the files named by accepted findings awaiting a re-audit, and the seeder plants no defect in them.
- [ ] When an auditor's every catalogue defect lies in such a file, the run picks another defect for that auditor or reports which auditor it could not seed. It never silently plants fewer than one per auditor.
- [ ] `RunAudit.ps1 -DryRun` lists the files the seeder must avoid.
- [ ] The change lands through an `audit` branch pull request with CI green on Windows, Linux and macOS.

## Notes

## Log

- 2026-10-12: Created.
- 2026-10-13: Backlog -> Doing. interactive session
