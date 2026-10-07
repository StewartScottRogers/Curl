---
id: BL-1624
title: Make the Curl.Protocol.Ssh.UnitTests host-key, key-exchange and negotiation tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1624 — Make the Curl.Protocol.Ssh.UnitTests host-key, key-exchange and negotiation tests write descriptive diagnostic output

## Goal

Every test in these files of `Curl.Protocol.Ssh.UnitTests` writes, through BL-1457's shared `TestDiagnostics` helper, the Arrange inputs that matter, its Act result and its assertion context (expected against actual, first differing byte or character), plus `PHASE` timings where it has distinct phases; no test's logic or assertions change. Files: HostKeys/, KeyExchange/ and Negotiation/ (about 80 test methods, counted 2026-10-07).

## Context

- Split from BL-1484: one task for the whole project (811 test methods in 94 files) was too big for one `/task-run`. BL-1484's Context applies in full: the line format and helper usage in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR; what matters for SSH (packets as `BYTES` with their decoded message number, negotiated algorithms, keys as hex, authentication steps, SFTP and SCP requests and replies, `PHASE` lines); output only; `BYTES` for large payloads; nothing OS-dependent. BL-1481 (`Curl.Protocol.Rtsp.UnitTests/RtspDiagnostics.cs`) is a worked example.
- A shared helper in this project (for example `SshDiagnostics.cs`) may be added or extended by whichever split task gets there first; the others reuse it.
- Classes: KnownHostsFileTests, Libssh2Base64Tests, SshHostKeyCheckerTests, SystemSshEphemeralKeySourceTests, SshAlgorithmNegotiatorTests, SshAlgorithmPreferencesTests, SshKexInitTests.

## Acceptance criteria

- [x] `dotnet test Curl.Protocol.Ssh.UnitTests --filter "TestCategory!=Integration&(FullyQualifiedName~<class>|...)" --logger "console;verbosity=detailed"` over this task's classes prints an `END` line for every test it runs, and piping that output to `Select-String -Pattern 'END .*(\(arrange 0,|, act 0,|, assert 0\))'` prints nothing.
- [x] The filtered run's test count is unchanged, and in this task's files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches (`Select-String -AllMatches`) are no lower than before; before and after numbers are recorded in Notes.
- [x] `dotnet build Curl.Protocol.Ssh.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.Ssh.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Protocol.Ssh.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Counts in this task's test files (HostKeys/, KeyExchange/, Negotiation/), before -> after: `Assert.` 149 -> 149, `[TestMethod` 80 -> 80, `[DataRow(` 101 -> 101. The filtered run over the seven classes runs 158 tests before and after (no attribute changed); every one prints an `END` line and none has a zero ARRANGE, ACT or ASSERT count.
- Added `Negotiation/SshNegotiationDiagnostics.cs` (a `KEXINIT`'s name-lists, the agreed algorithms, list DIFFs); host-key tests reuse BL-1622's `SshAuthenticationDiagnostics` for failures, `-v` lines, message names and escaped text.
- Choice: the tests that only check that a host key is accepted (no exception) now end in `ASSERT outcome: expected accepted, actual accepted`, written after the call returns, so their END line shows an assert count; the MSTest assertions themselves are unchanged.
- SLOW: no test printed a `SLOW:` line. The slowest phase is `CreateSntrup761KeyPair_FillsAFreshKeyPairThatDecapsulates`'s `PHASE key pairs` (about 600 ms for two sntrup761 key pairs), well inside the 3000 ms budget; no follow-up task.
- Verified: `dotnet build Curl.Protocol.Ssh.UnitTests -warnaserror` clean; `dotnet test Curl.Protocol.Ssh.UnitTests --filter "TestCategory!=Integration"` 1673 passed; `dotnet format whitespace --verify-no-changes` clean.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every host-key, key-exchange and negotiation test in Curl.Protocol.Ssh.UnitTests writes ARRANGE, ACT and ASSERT/DIFF diagnostics
