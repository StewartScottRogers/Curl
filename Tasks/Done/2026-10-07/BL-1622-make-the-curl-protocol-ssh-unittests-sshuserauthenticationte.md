---
id: BL-1622
title: Make the Curl.Protocol.Ssh.UnitTests SshUserAuthenticationTests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1622 — Make the Curl.Protocol.Ssh.UnitTests SshUserAuthenticationTests write descriptive diagnostic output

## Goal

Every test in these files of `Curl.Protocol.Ssh.UnitTests` writes, through BL-1457's shared `TestDiagnostics` helper, the Arrange inputs that matter, its Act result and its assertion context (expected against actual, first differing byte or character), plus `PHASE` timings where it has distinct phases; no test's logic or assertions change. Files: Authentication/SshUserAuthenticationTests.cs and its partials (.Agent, .KeyFileCertificate, .PublicKey, .RsaCertificate, .VerboseLines, .WinCngKeyTypes) (about 103 test methods, counted 2026-10-07).

## Context

- Split from BL-1484: one task for the whole project (811 test methods in 94 files) was too big for one `/task-run`. BL-1484's Context applies in full: the line format and helper usage in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR; what matters for SSH (packets as `BYTES` with their decoded message number, negotiated algorithms, keys as hex, authentication steps, SFTP and SCP requests and replies, `PHASE` lines); output only; `BYTES` for large payloads; nothing OS-dependent. BL-1481 (`Curl.Protocol.Rtsp.UnitTests/RtspDiagnostics.cs`) is a worked example.
- A shared helper in this project (for example `SshDiagnostics.cs`) may be added or extended by whichever split task gets there first; the others reuse it.
- Classes: SshUserAuthenticationTests.

## Acceptance criteria

- [x] `dotnet test Curl.Protocol.Ssh.UnitTests --filter "TestCategory!=Integration&(FullyQualifiedName~<class>|...)" --logger "console;verbosity=detailed"` over this task's classes prints an `END` line for every test it runs, and piping that output to `Select-String -Pattern 'END .*(\(arrange 0,|, act 0,|, assert 0\))'` prints nothing.
- [x] The filtered run's test count is unchanged, and in this task's files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches (`Select-String -AllMatches`) are no lower than before; before and after numbers are recorded in Notes.
- [x] `dotnet build Curl.Protocol.Ssh.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.Ssh.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Protocol.Ssh.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Approach: the class's shared helpers write the lines, so most tests needed no edit. `Script`, `Connect`, `ScriptRecordingLines` and `ConnectWithPresetAsync` write ARRANGE lines (server messages by decoded message number, then each as `BYTES`; backend, agent, key files, server identification) and a `PHASE key exchange`; `AuthenticationMessagesAsync` and `WrittenPayloads` write the client messages as ACT and `BYTES`; `AssertWritten`, `AssertMethods`, `AssertSigned`, `AssertLines`, `AssertAgentLines`, `AssertLoginDenied`, `AssertAuthenticationFailure` and `AssertServiceRequestFailsAsync` write ACT, ASSERT and DIFF lines. The 27 tests the helpers did not cover got their own lines. Those helpers became instance methods because they read the test's `TestContext`.
- New helper `Curl.Protocol.Ssh.UnitTests/Authentication/SshAuthenticationDiagnostics.cs` (decoded message names, failure exit code and text, `-v` lines). It is named for authentication, not `SshDiagnostics.cs`, so it cannot collide with the project-wide helper a sibling split task may add.
- `Connect` now takes the server's bytes, not a `ScriptedConnection`, so it can write them; its 7 callers changed to match. Their logic is the same.
- Counts in this task's 7 files, before -> after: `Assert.` 149 -> 150, `[TestMethod` 106 -> 106, `[DataRow(` 109 -> 109. The filtered run was 178 tests before and is 178 after; all 178 print an `END` line and none has arrange, act or assert 0. The full project fast run is 1673 passed.
- SLOW lines (all bcrypt_pbkdf decrypting an encrypted OpenSSH key; the time is in the new `PHASE authentication`, key exchange 3-4 ms):
  - `AuthenticateAsync_EncryptedEd25519KeyWithItsPassphrase_AsksThenSignsWithSshEd25519`: 37240 ms, authentication 37199 ms.
  - `AuthenticateAsync_EncryptedKeyNotOpenedOnWinCng_ReportsReasonUnknownAsMeasured` (a wrong --pass): 28105 ms, authentication 28082 ms.
  - `AuthenticateAsync_EncryptedOpenSshKeyNotOpened_SendsNoPublicKeyRequestAsMeasured` (a wrong --pass): 26341 ms, authentication 26316 ms.
  - `AuthenticateAsync_EncryptedKeyNotOpenedOnOpenSsl_ReportsTheUnrecognizedKeyFileAsMeasured` (a wrong --pass): 24418 ms, authentication 24394 ms.
  - The performance problem is already filed as BL-1558 (make BcryptPbkdf's bcrypt_hash fast), so no new task.


## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every SshUserAuthenticationTests test writes ARRANGE, ACT and ASSERT diagnostics; 178 tests green
