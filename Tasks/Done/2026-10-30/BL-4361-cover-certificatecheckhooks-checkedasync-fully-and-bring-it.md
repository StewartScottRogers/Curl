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
completed: 2026-10-30
---
# BL-4361 — Cover CertificateCheckHooks.CheckedAsync fully and bring it under complexity 10

## Goal

CertificateCheckHooks.CheckedAsync is split below complexity 10 and fully covered.

## Context

Quality gate failure in Curl.Extension.Abstractions.UnitLibrary (see Notes).

## Acceptance criteria

- [x] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

Measured by Measure-CodeQuality.ps1 on 2026-10-30: `CertificateCheckHooks.CheckedAsync` (CertificateCheckHooks.cs:77) is at line 95%, branch 91.67%, cyclomatic complexity 12; line 98 is uncovered. Split the method below complexity 10 and test every branch.

Criterion met: Measure-CodeQuality.ps1 reports Curl.Extension.Abstractions.UnitLibrary at 100% line, 100% branch, no member over complexity 10.

Done: split into CheckedAsync, FirstRefusalOrDisposeAsync and FirstRefusalAsync; the catch no longer awaits, so no uncovered compiler rethrow stub (rethrow is `throw thrown`). Added two tests with an asynchronously disposing connection.

## Log

- 2026-10-30: Created.
- 2026-10-30: Backlog -> Doing. Criteria at claim: fbecace0.
- 2026-10-30: Doing -> Done. Split CheckedAsync below complexity 10; library at 100% line and branch
