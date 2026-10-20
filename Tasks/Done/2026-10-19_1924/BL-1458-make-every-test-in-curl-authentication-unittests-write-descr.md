---
id: BL-1458
title: Make every test in Curl.Authentication.UnitTests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Authentication.UnitTests]
requirement: none
created: 2026-10-04
completed: 2026-10-19
---
# BL-1458 — Make every test in Curl.Authentication.UnitTests write descriptive diagnostic output

## Goal

Every test in `Curl.Authentication.UnitTests` writes, through BL-1457's shared `TestDiagnostics` helper, the Arrange inputs that matter, its Act result and its assertion context (expected against actual, first differing byte or character), plus `PHASE` timings where it has distinct phases, so an AI reading a failed or slow test's log can debug it or understand its time without re-running it; no test's logic or assertions change.

## Context

- Stewart's request, plan approved 2026-10-04: every unit test writes enough descriptive console output that an AI reading a failed or slow test's log can debug it without re-running it; slow budget 3 seconds per test. One task per test project; this one is `Curl.Authentication.UnitTests`, which tests `Curl.Authentication.UnitLibrary`.
- BL-1457 links the root `TestDiagnostics.cs` into every test project and writes each test's `START`, `END ... (arrange <a>, act <b>, assert <c>)` and `SLOW:` lines itself. The line format (`ARRANGE`, `ACT`, `ASSERT`, `BYTES`, `DIFF`, `PHASE`) and how a test calls the helper are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR; follow them and add no prefix of your own.
- Size, counted 2026-10-04 by matching `[TestMethod]`, `[TestMethod(` and `[DataTestMethod]` in the project's `.cs` files: 426 test methods in 33 files, with 534 `[DataRow(` lines. Solution-wide, 21 test files reference `TestContext` (mostly for its `CancellationToken`) and only 3, all in `Curl.Console.UnitTests`, write any output today.
- What matters here: the scheme and challenge header the authenticator receives, the credentials and options it is given (test credentials are fake, so print them), the `Authorization` or `Proxy-Authorization` value it produces, and in tests driven by `Fakes/ScriptedSecurityContext.cs` each scripted token as `BYTES`.
- Output only. No test method, data row, assertion or arrange step is removed, weakened or changed in what it tests. A shared fake or helper in this project may write the lines for the tests that use it, as long as each test's `END` line counts them.
- Keep the log readable: large payloads go through `BYTES` (which caps itself), never a loop printing thousands of lines. Nothing printed may make a test depend on the operating system.

## Acceptance criteria

- [x] This task is obsolete, and closes without a change: it was split into BL-1706 to BL-1710, all Done.

## Notes

- Sized for one run: 426 test methods in 33 files.

- 2026-10-19: Stewart had it closed as obsolete. Its criteria were replaced by the one above while it was in the Backlog, and the evidence was checked mechanically first.

## Log

- 2026-10-04: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Blocked. Stewart: dark factory stopped at its 5.91 US dollar cost cap (2.7 times the median run, at most -TaskBudgetUsd), so split the task; see Z:\repos\Curl.logs\BL-1458-20261006-200306-L1.jsonl
- 2026-10-07: Blocked -> Deferred. Split into BL-1706 to BL-1710 (chained) after the run hit its cost cap; nothing left to do here. Do not reopen.
- 2026-10-19: Deferred -> Backlog. Stewart: close it as obsolete (it was split into BL-1706 to BL-1710, all Done).
- 2026-10-19: Backlog -> Doing. Interactive session: closing it as obsolete. Criteria at claim: da38bf1b.
- 2026-10-19: Doing -> Done. Obsolete: it was split into BL-1706 to BL-1710, all Done.
