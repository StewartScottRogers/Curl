---
id: BL-1059
title: Skip ConfigureAwait(false) in Invoke-MutationTest.ps1: flipping it is an equivalent mutant that lowers the score and wastes the sample
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1004]
touches: [Audit/Tools/Invoke-MutationTest.ps1]
lane: no
requirement: none
created: 2026-09-30
completed: 2026-09-30
---
# BL-1059 — Skip ConfigureAwait(false) in Invoke-MutationTest.ps1: flipping it is an equivalent mutant that lowers the score and wastes the sample

## Goal

`Invoke-MutationTest.ps1` never mutates the `false` in `ConfigureAwait(false)`, so its sample and score count only mutants that could change behaviour.

## Context

Found by the audit-quality trial run of BL-1009 (2026-09-30) on `Curl.Protocol.Dict.UnitLibrary`, seed 1: of 35 mutants, all 9 survivors were `ConfigureAwait(false)` flipped to `ConfigureAwait(true)` in `DictProtocolHandler.cs`. In a console app with no synchronization context that change cannot alter behaviour, so no test can kill it: an equivalent mutant. The auditor rightly filed nothing, but the score came out 0.7273 instead of 23 killed + 1 timed out of 24 real mutants (1.0), and each flip took a slot of `-MaxMutants`. Every async library in Curl uses `ConfigureAwait(false)` throughout, so this skews every score.

## Acceptance criteria

- [x] The site finder skips the `false` inside `ConfigureAwait(false)` (and `true` inside `ConfigureAwait(true)`); other `true`/`false` literals are still sites.
- [x] `-SelfTest` gains a case for `await x.ConfigureAwait(false);` giving no site, and one for `return false;` still giving one; all cases PASS under Windows PowerShell 5.1 and PowerShell 7.
- [x] The header help lists the exclusion with its reason.

## Notes

- Audit branch commit 690e77e4. -SelfTest 19 PASS, 0 FAIL under Windows PowerShell 5.1 and PowerShell 7.6.6, including await x.ReadAsync(b).ConfigureAwait(false); giving no site and return false; still giving false@7. The header help lists the exclusion and its reason.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Invoke-MutationTest.ps1 never mutates the true or false inside ConfigureAwait(...).
