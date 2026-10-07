---
id: BL-1618
title: Make Curl.Protocol.Http.UnitTests' HttpProtocolHandlerTests.WeirdHeaderLine and HttpProtocolHandlerTests.cs tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1618 — Make Curl.Protocol.Http.UnitTests' HttpProtocolHandlerTests.WeirdHeaderLine and HttpProtocolHandlerTests.cs tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Protocol.Http.UnitTests` files (2 files, 50 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `HttpProtocolHandlerTests.WeirdHeaderLine.cs`, `HttpProtocolHandlerTests.cs`.

## Context

- Split from BL-1476 (one task per range of files, as its Notes direct); BL-1476 keeps the whole-project checks and depends on this task. Follow BL-1476's Context: what matters in this project (request line and headers, the scripted response, the parsed status, headers and body, each redirect or authentication step, the `CurlExitCode` with its error text), `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system. BL-1478 (`Curl.Protocol.Ldap.UnitTests`) shows the pattern.
- `HttpProtocolHandlerTests` is one partial class (563 test methods) spread over seven tasks, BL-1612 to BL-1618. "Its filter" below is `FullyQualifiedName~Curl.Protocol.Http.HttpProtocolHandlerTests.`, and the first criterion's check applies only to the `END` lines of test methods declared in this task's files. `HttpProtocolHandlerTests.cs` holds the class's shared helpers; one of them may write lines for every test that uses it.

## Acceptance criteria

- [x] `dotnet test Curl.Protocol.Http.UnitTests --filter "<its filter>" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none for a test method declared in these files matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Protocol.Http.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.Http.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Protocol.Http.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Counts before -> after (`Assert.`, `[TestMethod`, `[DataRow(`): `HttpProtocolHandlerTests.WeirdHeaderLine.cs` 10/4/3 -> 10/4/3; `HttpProtocolHandlerTests.cs` 155/46/27 -> 155/46/27. No assertion, test method or data row changed.
- The class filter ran 798 tests (785 passed, 13 skipped); every test method declared in these two files printed an `END` line, and no `END` line in the run has a zero arrange, act or assert count.
- The four `Throws` tests (the two expression-bodied constructor tests, `ExecuteAsync_NullContext_Throws` and `ExecuteAsync_Cancelled_Throws`) now keep the exception `Assert.ThrowsExactly` returns so they can write it; the assertion itself is unchanged. `AssertWeirdHeaderLineAsync` became an instance method so it can write through `Diagnostics`. The NUL byte in the scripted response is printed as `\0`.
- No test printed a `SLOW:` line; no follow-up task needed.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. All 50 test methods in the two files write ARRANGE, ACT and ASSERT lines; build clean, 1846 fast tests pass
