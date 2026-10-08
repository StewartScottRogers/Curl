---
id: BL-1630
title: Make the Curl.Protocol.Ssh.UnitTests transport tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1630 — Make the Curl.Protocol.Ssh.UnitTests transport tests write descriptive diagnostic output

## Goal

Every test in these files of `Curl.Protocol.Ssh.UnitTests` writes, through BL-1457's shared `TestDiagnostics` helper, the Arrange inputs that matter, its Act result and its assertion context (expected against actual, first differing byte or character), plus `PHASE` timings where it has distinct phases; no test's logic or assertions change. Files: Transport/ (about 102 test methods, counted 2026-10-07).

## Context

- Split from BL-1484: one task for the whole project (811 test methods in 94 files) was too big for one `/task-run`. BL-1484's Context applies in full: the line format and helper usage in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR; what matters for SSH (packets as `BYTES` with their decoded message number, negotiated algorithms, keys as hex, authentication steps, SFTP and SCP requests and replies, `PHASE` lines); output only; `BYTES` for large payloads; nothing OS-dependent. BL-1481 (`Curl.Protocol.Rtsp.UnitTests/RtspDiagnostics.cs`) is a worked example.
- A shared helper in this project (for example `SshDiagnostics.cs`) may be added or extended by whichever split task gets there first; the others reuse it.
- Classes: SshConnectionReaderTests, SshIdentificationExchangeTests, SshPacketReaderTests, SshPacketWriterTests, SshTransportTests, SshWireReaderTests, SshWireWriterTests.

## Acceptance criteria

- [x] `dotnet test Curl.Protocol.Ssh.UnitTests --filter "TestCategory!=Integration&(FullyQualifiedName~<class>|...)" --logger "console;verbosity=detailed"` over this task's classes prints an `END` line for every test it runs, and piping that output to `Select-String -Pattern 'END .*(\(arrange 0,|, act 0,|, assert 0\))'` prints nothing.
- [x] The filtered run's test count is unchanged, and in this task's files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches (`Select-String -AllMatches`) are no lower than before; before and after numbers are recorded in Notes.
- [x] `dotnet build Curl.Protocol.Ssh.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.Ssh.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Protocol.Ssh.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Filter used for every run: `FullyQualifiedName~Curl.Protocol.Ssh.Transport.` - the
  namespace holds exactly this task's seven classes (nine files). Before: 255 tests.
- Counts before (`Assert.` / `[TestMethod` / `[DataRow(`): SshConnectionReaderTests 13/9/0,
  SshIdentificationExchangeTests 13/7/6, SshPacketReaderTests 19/10/24,
  SshPacketWriterTests 13/7/12, SshTransportTests 33/14/8,
  SshTransportTests.HostKeyCertificates 7/7/21, SshTransportTests.KeyExchange 51/38/109,
  SshWireReaderTests 14/8/6, SshWireWriterTests 2/2/4; total 165/102/190.
- Counts after: SshConnectionReaderTests 14/9/0, SshIdentificationExchangeTests 13/7/6,
  SshPacketReaderTests 22/10/24, SshPacketWriterTests 13/7/12, SshTransportTests 33/14/8,
  SshTransportTests.HostKeyCertificates 7/7/21, SshTransportTests.KeyExchange 51/38/109,
  SshWireReaderTests 18/8/6, SshWireWriterTests 2/2/4; total 173/102/190. After: 255 tests,
  255 `END` lines, none with arrange, act or assert 0. Full project: 1673 passed.
- The extra `Assert.` matches are throw-only tests that now also assert the caught
  exception's type, and inlined assertions hoisted into locals; no logic changed.
- Reused `SshAuthenticationDiagnostics` (BL-1622) for message names, escaped text and
  failures rather than adding a second helper. `AssertKeyExchangeFailsAsync` became an
  instance method so it writes the act and assert lines for the 20+ failure tests that
  share it; `AssertFailsAsync` takes the test's diagnostics.
- SLOW: under nine-lane load one run printed `SLOW:` for both Linux-preset rows of
  `ExchangeKeysAsync_GroupExchangeOnEachPreset_AsksForThePresetsSizesAndAcceptsAPrimeAtItsMaximum`
  (3967 ms and 4067 ms). Breakdown on a later run (3088 ms and 3114 ms): `PHASE script the
  server` 1368 / 1492 ms (the scripted server's 8192-bit group18 modular exponentiation),
  `PHASE key exchange` 952 / 965 ms (the client's own 8192-bit Diffie-Hellman). The final
  run printed no `SLOW:` line. Not a performance problem: an 8192-bit modular
  exponentiation is that cost, and the Windows rows (4096-bit group16) take ~300 ms. No
  follow-up task.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every Curl.Protocol.Ssh.UnitTests Transport test writes ARRANGE, ACT and ASSERT/DIFF diagnostics
