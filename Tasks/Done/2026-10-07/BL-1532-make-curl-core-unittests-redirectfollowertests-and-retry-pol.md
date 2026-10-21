---
id: BL-1532
title: Make Curl.Core.UnitTests' RedirectFollowerTests and retry policy tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Core.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1532 — Make Curl.Core.UnitTests' RedirectFollowerTests and retry policy tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Core.UnitTests` files writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `RedirectFollowerTests.cs` (77 tests), `RedirectPolicyTests.cs`, `RetryAfterHeaderTests.cs`, `RetryPolicyTests.cs` (85 test methods, counted 2026-10-07).

## Context

- Split from BL-1463 (one per range of files, as its Notes direct); BL-1463 keeps the whole-project checks and depends on this task. Read BL-1463's Context: line format and helper usage are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR; add no prefix of your own; output only; redirect chains and recorded transfer events are worth printing; nothing printed may depend on the operating system.

## Acceptance criteria

- [x] `dotnet test Curl.Core.UnitTests --filter "FullyQualifiedName~Curl.Core.RedirectFollowerTests.|FullyQualifiedName~RedirectPolicyTests|FullyQualifiedName~RetryAfterHeaderTests|FullyQualifiedName~RetryPolicyTests" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Core.UnitTests -warnaserror` is clean and `dotnet test Curl.Core.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Core.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Approach: `RedirectFollowerTests`' `Follow`, `FollowWith` and `FollowNonSeekableBody` helpers (now instance methods) go through one `FollowLogged`, which writes ARRANGE lines for the first request, the policy and the scripted responses, and ACT lines for the exit code, error, report and the redirect chain the handler saw. The three tests that build their own `RedirectFollower` (null arguments, Alt-Svc selector, credential selector) call `FollowLogged` or write their own lines. Every `Assert.*`/`CollectionAssert.*` call in the four files is preceded by a `Diagnostics.Assert` line with the same expected and actual values; those lines were inserted by a throwaway PowerShell script (not committed) rather than 200 hand edits, and the diff was checked to change no assertion.
- Counts before -> after (`Assert.`, `[TestMethod`, `[DataRow(`): RedirectFollowerTests 208/77/110 -> 208/77/110; RedirectPolicyTests 12/2/0 -> 12/2/0; RetryAfterHeaderTests 4/4/28 -> 4/4/28; RetryPolicyTests 7/2/0 -> 7/2/0.
- Filtered detailed run: 190 tests, 189 run and passed, 1 skipped by `OSCondition` (off-Windows only); 189 END lines, none with a zero count.
- SLOW: none printed.
- Fast tests: Curl.Core.UnitTests 1441 passed, 6 skipped, 0 failed.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. RedirectFollower, RedirectPolicy, RetryAfterHeader and RetryPolicy tests write ARRANGE, ACT and ASSERT diagnostics with the redirect chain
