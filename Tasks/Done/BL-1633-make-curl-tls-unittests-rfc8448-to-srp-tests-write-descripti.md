---
id: BL-1633
title: Make Curl.Tls.UnitTests' Rfc8448 to Srp tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Tls.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1633 — Make Curl.Tls.UnitTests' Rfc8448 to Srp tests write descriptive diagnostic output

## Goal

Every test in `Curl.Tls.UnitTests`' `Rfc8448ClientAuthenticationHandshakeTests`, `Rfc8448ClientHandshakeTests`, `Rfc8448HandshakeMessageTests`, `Rfc8448KeyScheduleTests`, `Rfc8448RecordTests`, `Rfc8448ResumedHandshakeTests`, `ServerHelloReplayStreamTests` and `SrpClientTests` (8 files, 63 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed.

## Context

- Split from BL-1489 (one task per range of files, as its Notes direct); BL-1489 keeps the whole-project checks and depends on this task. Follow BL-1489's Context for what matters in this project - here above all keys, secrets and transcript hashes as hex with a `DIFF` against the RFC 8448 values - and `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- Every class is in namespace `Curl.Tls`, flat in the project root. A shared fake or helper may write lines for the tests that use it, as long as each test's `END` line counts them.

## Acceptance criteria

- [x] `dotnet test Curl.Tls.UnitTests --filter "FullyQualifiedName~Curl.Tls.Rfc8448|FullyQualifiedName~Curl.Tls.ServerHelloReplayStreamTests.|FullyQualifiedName~Curl.Tls.SrpClientTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Tls.UnitTests -warnaserror` is clean and `dotnet test Curl.Tls.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Tls.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Counts before -> after (`Assert.`, `[TestMethod`, `[DataRow(`): Rfc8448ClientAuthenticationHandshakeTests 17/3/0 -> 17/3/0; Rfc8448ClientHandshakeTests 40/6/0 -> 40/6/0; Rfc8448HandshakeMessageTests 66/12/2 -> 66/12/2; Rfc8448KeyScheduleTests 1/13/0 -> 1/13/0; Rfc8448RecordTests 14/4/3 -> 14/4/3; Rfc8448ResumedHandshakeTests 32/7/0 -> 32/7/0; ServerHelloReplayStreamTests 19/4/0 -> 20/4/0; SrpClientTests 22/14/7 -> 23/14/7. The two increases are the text "checked by Assert.ThrowsExactly below" in the `ASSERT` line written before each run of `Assert.ThrowsExactly` calls, not new assertions.
- The filtered run prints 72 `END` lines (63 methods with their data rows), all `Passed`, none with a zero count. The whole project passes, 1283 tests; the solution builds clean and its fast tests pass.
- New shared helper `Rfc8448Diagnostics.cs` (extension methods on `TestDiagnostics`): `ArrangeHex` writes a trace input as hex (up to 64 bytes) or its length and `BYTES`; `ActAndDiffHex` writes a derived value the same way plus a `DIFF` against the trace's hex. Each file's `Hex`/`AssertHex`/`AssertSecret` helpers that ran inside a test became instance methods taking a `[CallerArgumentExpression]` label, so a line names what it shows, e.g. `ACT Schedule.DeriveFinishedKey(Hex(SimpleServerHandshakeTrafficSecret)): 008d...` and `DIFF ...: equal (64 characters)`. Static `Hex` helpers used by static fields and by `Tls13ClientStreamTests` (`TraceSettings`, `TraceRandom`) stayed static. Where an assertion called the code under test inline, the result now goes into a local first so the same value is printed and asserted; no expected value or assertion kind changed.
- `PHASE` lines wrap the handshakes (client authentication, HelloRetryRequest, byte-at-a-time flight, the section 3 session the resumed tests start from, the record-level connection); the longest seen was `PHASE handshake: 67 ms`.
- `SLOW:` lines: none.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Curl.Tls.UnitTests' Rfc8448 to Srp tests (72 runs) write ARRANGE, ACT and ASSERT or DIFF lines; no SLOW lines
