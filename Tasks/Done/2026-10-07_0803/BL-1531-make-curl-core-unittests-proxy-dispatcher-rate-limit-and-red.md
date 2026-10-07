---
id: BL-1531
title: Make Curl.Core.UnitTests' proxy, dispatcher, rate-limit and redirect side tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Core.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1531 — Make Curl.Core.UnitTests' proxy, dispatcher, rate-limit and redirect side tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Core.UnitTests` files writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `NoProxyMatcherTests.cs`, `ProtocolDispatcherTests.cs`, `ProxySelectorTests.cs`, `ProxySelectorDiagnosticLogTests.cs`, `ProxyUrlParserTests.cs`, `RateLimitedStreamTests.cs`, `RedirectDiagnosticLogTests.cs`, `RedirectFollowerDiagnosticLogTests.cs`, `RedirectFollowerHstsTests.cs`, `RedirectFollowerIssueAnotherRequestTests.cs`, `RedirectFollowerMethodSwitchLineTests.cs` (107 test methods, counted 2026-10-07).

## Context

- Split from BL-1463 (one per range of files, as its Notes direct); BL-1463 keeps the whole-project checks and depends on this task. Read BL-1463's Context: line format and helper usage are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR; add no prefix of your own; output only; redirect chains, recorded transfer events and diagnostic-log lines are worth printing; nothing printed may depend on the operating system.

## Acceptance criteria

- [x] `dotnet test Curl.Core.UnitTests --filter "FullyQualifiedName~NoProxyMatcherTests|FullyQualifiedName~ProtocolDispatcherTests|FullyQualifiedName~ProxySelector|FullyQualifiedName~ProxyUrlParserTests|FullyQualifiedName~RateLimitedStreamTests|FullyQualifiedName~RedirectDiagnosticLogTests|FullyQualifiedName~RedirectFollowerDiagnosticLogTests|FullyQualifiedName~RedirectFollowerHstsTests|FullyQualifiedName~RedirectFollowerIssueAnotherRequestTests|FullyQualifiedName~RedirectFollowerMethodSwitchLineTests" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Core.UnitTests -warnaserror` is clean and `dotnet test Curl.Core.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Core.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Counts in the 11 files, before -> after (grep line counts): `Assert.` 190 -> 190, `[TestMethod` 105 -> 105, `[DataRow(` 228 -> 228. Every original assertion is kept; where a value is now logged, the inline expression was moved into a local and asserted unchanged (`diagnostics.Assert(` lines are not counted as `Assert.`).
- The acceptance filter's `FullyQualifiedName~ProxySelector` also matches three `RedirectFollowerTests.FollowAsync_*HopProxySelector*` tests, which print `(arrange 0, act 0, assert 0)`. That file is not in this task's list; it belongs to BL-1532 (RedirectFollowerTests and retry policy), so it was left to that task to avoid two lanes editing one file. All 309 tests from this task's 11 files print non-zero counts; the run passed 312 of 312.
- No test printed a `SLOW:` line (the slowest took a few ms), so no follow-up task.
- Diagnostics print redirect chains, recorded transfer events and diagnostic-log lines joined with ` | `, so nothing printed depends on the operating system's line ending.
- `dotnet build Curl.Core.UnitTests -warnaserror` clean; `dotnet test Curl.Core.UnitTests --filter "TestCategory!=Integration"`: 1441 passed, 6 skipped, 0 failed; `dotnet format whitespace --verify-no-changes` clean on the changed files.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. All 309 tests in the 11 files write arrange, act and assert diagnostics; build clean, fast tests green
