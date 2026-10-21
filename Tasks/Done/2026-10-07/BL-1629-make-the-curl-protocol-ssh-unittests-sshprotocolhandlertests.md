---
id: BL-1629
title: Make the Curl.Protocol.Ssh.UnitTests SshProtocolHandlerTests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1629 — Make the Curl.Protocol.Ssh.UnitTests SshProtocolHandlerTests write descriptive diagnostic output

## Goal

Every test in these files of `Curl.Protocol.Ssh.UnitTests` writes, through BL-1457's shared `TestDiagnostics` helper, the Arrange inputs that matter, its Act result and its assertion context (expected against actual, first differing byte or character), plus `PHASE` timings where it has distinct phases; no test's logic or assertions change. Files: SshProtocolHandlerTests.cs and its 13 partials (SshProtocolHandlerTests.*.cs) at the project root (about 139 test methods, counted 2026-10-07).

## Context

- Split from BL-1484: one task for the whole project (811 test methods in 94 files) was too big for one `/task-run`. BL-1484's Context applies in full: the line format and helper usage in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR; what matters for SSH (packets as `BYTES` with their decoded message number, negotiated algorithms, keys as hex, authentication steps, SFTP and SCP requests and replies, `PHASE` lines); output only; `BYTES` for large payloads; nothing OS-dependent. BL-1481 (`Curl.Protocol.Rtsp.UnitTests/RtspDiagnostics.cs`) is a worked example.
- A shared helper in this project (for example `SshDiagnostics.cs`) may be added or extended by whichever split task gets there first; the others reuse it.
- Classes: SshProtocolHandlerTests.

## Acceptance criteria

- [x] `dotnet test Curl.Protocol.Ssh.UnitTests --filter "TestCategory!=Integration&(FullyQualifiedName~<class>|...)" --logger "console;verbosity=detailed"` over this task's classes prints an `END` line for every test it runs, and piping that output to `Select-String -Pattern 'END .*(\(arrange 0,|, act 0,|, assert 0\))'` prints nothing.
- [x] The filtered run's test count is unchanged, and in this task's files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches (`Select-String -AllMatches`) are no lower than before; before and after numbers are recorded in Notes.
- [x] `dotnet build Curl.Protocol.Ssh.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.Ssh.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Protocol.Ssh.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- New partial `SshProtocolHandlerTests.Diagnostics.cs` holds the class's `TestContext`, `ArrangeTransfer` (URL, user, upload bytes, quote commands, -l, max file size, range, proxy), `ActTransfer` (result, output bytes, server events, verbose transcript) and `AssertTextDiagnostic`. The shared run helpers (`RunAsync`, `RunResetAsync`, `RunFailingAsync`, `RunLoggingAsync`, `RunTracedAsync`, `RunRecordingLinesAsync`, `RunThroughProxyRecordingLinesAsync`) became instance methods and write ARRANGE and ACT lines; `RunAsync` also writes `PHASE transfer` and `PHASE session end`. `AssertFailure`, `AssertEvents`, `AssertFailedTeardown` and `AssertPartialDownload` write their own ASSERT and DIFF lines.
- Choice: tests that pin a long verbose transcript or diagnostic log write `AssertCheckedOutcomeDiagnostic()` before their first assertion - an ASSERT line with the transcript (or log) they check - rather than copying each long expected string into a second statement; the assertion's own message carries the expected text on failure. Tests with a short expected value write it exactly (exit code, result, error text with DIFF, port, block sizes).
- Before/after counts in this task's files: `Assert.` 209 -> 210, `[TestMethod` 119 -> 119, `[DataRow(` 88 -> 88. Filtered run: 186 tests before and after; 186 END lines, none with a zero count.
- SLOW: lines, all from the encrypted-key tests, whose time is the bcrypt_pbkdf key derivation in the key file (under nine lanes' load): `ExecuteAsync_EncryptedKeyWithAWrongPassphrase_IsExit67AsMeasured` 23783 ms, `ExecuteAsync_EncryptedEd25519KeyTheServerAuthorizes_AuthenticatesWithPublickey` 32248 ms (nearly all in `PHASE transfer`; `PHASE session end` 0 ms), `ExecuteAsync_EncryptedKeyAtVerbose_NoMessageContainsThePassPhraseOrAPrivateKeyByte` 38253 ms. The follow-up already exists: BL-1558 (make BcryptPbkdf's bcrypt hash fast); no new task filed.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every SshProtocolHandlerTests test writes ARRANGE, ACT and ASSERT diagnostic lines, with PHASE timings for whole transfers
