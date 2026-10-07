---
id: BL-1626
title: Make the Curl.Protocol.Ssh.UnitTests packet-protection and root helper tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1626 — Make the Curl.Protocol.Ssh.UnitTests packet-protection and root helper tests write descriptive diagnostic output

## Goal

Every test in these files of `Curl.Protocol.Ssh.UnitTests` writes, through BL-1457's shared `TestDiagnostics` helper, the Arrange inputs that matter, its Act result and its assertion context (expected against actual, first differing byte or character), plus `PHASE` timings where it has distinct phases; no test's logic or assertions change. Files: PacketProtection/ plus the root files ReceivedDataReportingStreamTests.cs, SshConnectionFailureTests.cs, SshDiagnosticLogTests.cs, SshWireDecodersTests.cs and SystemSshRandomSourceTests.cs (about 67 test methods, counted 2026-10-07).

## Context

- Split from BL-1484: one task for the whole project (811 test methods in 94 files) was too big for one `/task-run`. BL-1484's Context applies in full: the line format and helper usage in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR; what matters for SSH (packets as `BYTES` with their decoded message number, negotiated algorithms, keys as hex, authentication steps, SFTP and SCP requests and replies, `PHASE` lines); output only; `BYTES` for large payloads; nothing OS-dependent. BL-1481 (`Curl.Protocol.Rtsp.UnitTests/RtspDiagnostics.cs`) is a worked example.
- A shared helper in this project (for example `SshDiagnostics.cs`) may be added or extended by whichever split task gets there first; the others reuse it.
- Classes: every class in PacketProtection/ (AesCtrSshCipherTests, AesGcmPacketProtectionTests, CbcSshCipherTests, ChaCha20Poly1305PacketProtectionTests, CipherAndMacPacketProtectionTests, Rc4SshCipherTests, SshMacTests, SshPacketProtectionsTests, SshPlainPacketProtectionTests) plus ReceivedDataReportingStreamTests, SshConnectionFailureTests, SshDiagnosticLogTests, SshWireDecodersTests, SystemSshRandomSourceTests.

## Acceptance criteria

- [x] `dotnet test Curl.Protocol.Ssh.UnitTests --filter "TestCategory!=Integration&(FullyQualifiedName~<class>|...)" --logger "console;verbosity=detailed"` over this task's classes prints an `END` line for every test it runs, and piping that output to `Select-String -Pattern 'END .*(\(arrange 0,|, act 0,|, assert 0\))'` prints nothing.
- [x] The filtered run's test count is unchanged, and in this task's files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches (`Select-String -AllMatches`) are no lower than before; before and after numbers are recorded in Notes.
- [x] `dotnet build Curl.Protocol.Ssh.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.Ssh.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Protocol.Ssh.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Helpers: reused BL-1625's `Keys/SshKeyDiagnostics` (`ActBytes`, `AssertBytes`, `ActAndAssertThrown`) and added `PacketProtection/SshPacketDiagnostics.cs` with `ArrangePacket` (ARRANGE line decoding packet_length, padding_length and message number, then BYTES) and `AssertHex` (expected hex against actual bytes, then DIFF). Tests that compared inline results (`Assert.AreEqual(x, f())`) now hold the result in a local first so it can be written; the calls and every assertion are kept.
- The round-trip test in `SshPacketProtectionsTests` writes `PHASE write` and `PHASE read` lines (3-14 ms each).
- Counts in the task's 14 test files, before -> after: `Assert.` 121 -> 121, `[TestMethod` 62 -> 62, `[DataRow(` 96 -> 96. The filtered run: 138 tests, 138 `END` lines, none with arrange, act or assert 0.
- `dotnet build Curl.Protocol.Ssh.UnitTests -warnaserror` clean; the project's fast tests: 1673 passed.
- No test printed a `SLOW:` line.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. The packet-protection and root helper tests in Curl.Protocol.Ssh.UnitTests write ARRANGE, ACT, ASSERT, BYTES and PHASE diagnostics
