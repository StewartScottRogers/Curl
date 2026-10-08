---
id: BL-1489
title: Make every test in Curl.Tls.UnitTests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457, BL-1631, BL-1632, BL-1633, BL-1634, BL-1635, BL-1636, BL-1637, BL-1638, BL-1639, BL-1640, BL-1641, BL-1642, BL-1643, BL-1644]
touches: [Curl.Tls.UnitTests]
requirement: none
created: 2026-10-04
completed:
---
# BL-1489 — Make every test in Curl.Tls.UnitTests write descriptive diagnostic output

## Goal

Every test in `Curl.Tls.UnitTests` writes, through BL-1457's shared `TestDiagnostics` helper, the Arrange inputs that matter, its Act result and its assertion context (expected against actual, first differing byte or character), plus `PHASE` timings where it has distinct phases, so an AI reading a failed or slow test's log can debug it or understand its time without re-running it; no test's logic or assertions change.

## Context

- Stewart's request, plan approved 2026-10-04: every unit test writes enough descriptive console output that an AI reading a failed or slow test's log can debug it without re-running it; slow budget 3 seconds per test. One task per test project; this one is `Curl.Tls.UnitTests`, which tests `Curl.Tls.UnitLibrary`.
- BL-1457 links the root `TestDiagnostics.cs` into every test project and writes each test's `START`, `END ... (arrange <a>, act <b>, assert <c>)` and `SLOW:` lines itself. The line format (`ARRANGE`, `ACT`, `ASSERT`, `BYTES`, `DIFF`, `PHASE`) and how a test calls the helper are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR; follow them and add no prefix of your own.
- Size, counted 2026-10-04 by matching `[TestMethod]`, `[TestMethod(` and `[DataTestMethod]` in the project's `.cs` files: 747 test methods in 50 files, with 553 `[DataRow(` lines. Solution-wide, 21 test files reference `TestContext` (mostly for its `CancellationToken`; `Rfc8448RecordTests.cs` here is one) and only 3, all in `Curl.Console.UnitTests`, write any output today.
- What matters here: records and handshake messages exchanged through `ScriptedTransport.cs` as `BYTES` with their decoded content and handshake type, the negotiated version, cipher suite and group, keys, secrets and transcript hashes as hex with a `DIFF` against RFC 8448 values where a test uses them, alerts with their codes, and `PHASE` lines for each handshake flight.
- Output only. No test method, data row, assertion or arrange step is removed, weakened or changed in what it tests. A shared fake or helper in this project may write the lines for the tests that use it, as long as each test's `END` line counts them.
- Keep the log readable: large payloads go through `BYTES` (which caps itself), never a loop printing thousands of lines. Nothing printed may make a test depend on the operating system.

## Acceptance criteria

- [ ] `dotnet test Curl.Tls.UnitTests --filter "TestCategory!=Integration" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs (as many `END` lines as the run's total test count), and piping that output to `Select-String -Pattern 'END .*(\(arrange 0,|, act 0,|, assert 0\))'` prints nothing: every test wrote at least one `ARRANGE`, one `ACT` and one `ASSERT` or `DIFF` line.
- [ ] The run's total test count is unchanged, and in `Curl.Tls.UnitTests` (excluding `obj`) the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches (each counted with `Select-String -AllMatches`) are no lower than before the task; the before and after numbers are recorded in Notes.
- [ ] `dotnet build Curl.Tls.UnitTests -warnaserror` is clean and `dotnet test Curl.Tls.UnitTests --filter "TestCategory!=Integration"` passes.
- [ ] The task's commits change only files under `Curl.Tls.UnitTests/` and this task file.
- [ ] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; for each that is a real performance problem a follow-up task is filed and its ID is in Notes.

## Notes

- Large: 747 test methods in 50 files is probably more than one `/task-run` can finish. Stewart asked for one task per project, so it is filed whole. If the runner judges it too big, it splits it before changing any test: it files tasks that each cover a range of the project's files by name (each `-Pipeline direct -DependsOn BL-1457 -Touches Curl.Tls.UnitTests`, with these criteria limited to its files' classes through `--filter "FullyQualifiedName~<class>"`), adds them to this task's `depends-on`, and moves this task back to `Backlog`; this task then only runs the whole-project checks above.
- 2026-10-07 (lane 6): split as above, before any test changed. Counted 2026-10-07 by the same patterns: 747 test methods in 50 test files (before counts, whole project: 1655 `Assert.`, 747 `[TestMethod`, 553 `[DataRow(`), far more than one run's time and token budget. Every class is in namespace `Curl.Tls`, flat in the project root, so the 14 tasks cover contiguous name ranges of 36 to 68 methods each, filtered by class name with a trailing dot so `KeyShareTests.` never matches `KeyShareKnownAnswerTests`: BL-1631 (A to H), BL-1632 (K to O), BL-1633 (Rfc8448 to Srp), BL-1634 (Tls12 Cbc to ClientConnection), BL-1635 (Tls12ClientHandshakeFailureTests), BL-1636 (Tls12ClientHandshakeTests), BL-1637 (Tls12 ClientStream to RecordProtection), BL-1638 (Tls12 Signature, SrpHandshake), BL-1639 (Tls13 CertificateCompression to ClientHelloBuilder), BL-1640 (Tls13ClientHandshakeFailureTests), BL-1641 (Tls13 ClientStream, EncryptedClientHello), BL-1642 (Tls13 KeySchedule to ResumptionHandshake), BL-1643 (TlsClientConnection to TlsPrf), BL-1644 (TlsReaderWriter to TlsSignature). Once they are all Done, this task runs only the whole-project checks and records the before and after counts.

## Log

- 2026-10-04: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Backlog. Split into BL-1631 to BL-1644 (one per range of test files); this task runs only the whole-project checks once they are Done
- 2026-10-07: Backlog -> Doing.
