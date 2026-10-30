---
id: BL-4361
title: Cover CertificateCheckHooks.CheckedAsync fully and bring it under complexity 10
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extension.Abstractions.UnitLibrary/Certificates/CertificateCheckHooks.cs, Curl.Extension.Abstractions.UnitTests/Certificates]
requirement: none
created: 2026-10-30
completed:
---
# BL-4361 — Cover CertificateCheckHooks.CheckedAsync fully and bring it under complexity 10

## Goal

<!-- One sentence: the observable outcome once this task is done. -->

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [ ] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

Measured by Measure-CodeQuality.ps1 on 2026-10-30: `CertificateCheckHooks.CheckedAsync` (CertificateCheckHooks.cs:77) is at line 95%, branch 91.67%, cyclomatic complexity 12; line 98 is uncovered. Split the method below complexity 10 and test every branch.

## Log

- 2026-10-30: Created.
- 2026-10-30: Backlog -> Doing. Criteria at claim: fbecace0.
