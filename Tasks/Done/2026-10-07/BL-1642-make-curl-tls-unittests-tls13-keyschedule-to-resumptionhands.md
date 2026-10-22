---
id: BL-1642
title: Make Curl.Tls.UnitTests' Tls13 KeySchedule to ResumptionHandshake tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Tls.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1642 — Make Curl.Tls.UnitTests' Tls13 KeySchedule to ResumptionHandshake tests write descriptive diagnostic output

## Goal

Every test in `Curl.Tls.UnitTests`' `Tls13KeyScheduleTests`, `Tls13PostHandshakeAuthenticationTests`, `Tls13RecordProtectionTests`, `Tls13ResumptionConnectionTests` and `Tls13ResumptionHandshakeTests` (5 files, 60 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed.

## Context

- Split from BL-1489 (one task per range of files, as its Notes direct); BL-1489 keeps the whole-project checks and depends on this task. Follow BL-1489's Context for what matters in this project, and `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- Every class is in namespace `Curl.Tls`, flat in the project root. A shared fake or helper may write lines for the tests that use it, as long as each test's `END` line counts them.

## Acceptance criteria

- [x] `dotnet test Curl.Tls.UnitTests --filter "FullyQualifiedName~Curl.Tls.Tls13KeyScheduleTests.|FullyQualifiedName~Curl.Tls.Tls13PostHandshakeAuthenticationTests.|FullyQualifiedName~Curl.Tls.Tls13RecordProtectionTests.|FullyQualifiedName~Curl.Tls.Tls13ResumptionConnectionTests.|FullyQualifiedName~Curl.Tls.Tls13ResumptionHandshakeTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Tls.UnitTests -warnaserror` is clean and `dotnet test Curl.Tls.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Tls.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Before and after, per file (`Assert.` / `[TestMethod` / `[DataRow(`): KeySchedule 13/8/0, PostHandshakeAuthentication 28/7/0, RecordProtection 22/11/9, ResumptionConnection 48/13/0, ResumptionHandshake 45/21/7 - totals 156/60/16 before and after; no assertion changed.
- The filtered run prints 71 `END` lines (60 methods, data rows included), none with a zero count; all 71 pass, and the whole project passes 1283 of 1283.
- Handshake tests time `PHASE handshake`, `post-handshake authentication`, `first session` and `resumption handshake` (the longest about 130 ms). No test printed a `SLOW:` line, so no follow-up task.
- Shared private helpers write lines for the tests that use them: `WriteResumption`, `WriteSession`, `WriteExtensions`, `WriteOutcome`, and `TimedFirstSessionAsync` / `TimedResumeAsync`, which wrap the existing helpers in phases. Random content is printed by length only, so the output is the same on every run and platform.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. The 60 Tls13 key schedule, post-handshake auth, record protection and resumption tests write ARRANGE, ACT, ASSERT and PHASE diagnostics
