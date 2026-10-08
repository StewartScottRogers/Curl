---
id: BL-1484
title: Make every test in Curl.Protocol.Ssh.UnitTests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457, BL-1622, BL-1623, BL-1624, BL-1625, BL-1626, BL-1627, BL-1628, BL-1629, BL-1630]
touches: [Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-10-04
completed: 2026-10-07
---
# BL-1484 — Make every test in Curl.Protocol.Ssh.UnitTests write descriptive diagnostic output

## Goal

Every test in `Curl.Protocol.Ssh.UnitTests` writes, through BL-1457's shared `TestDiagnostics` helper, the Arrange inputs that matter, its Act result and its assertion context (expected against actual, first differing byte or character), plus `PHASE` timings where it has distinct phases, so an AI reading a failed or slow test's log can debug it or understand its time without re-running it; no test's logic or assertions change.

## Context

- Stewart's request, plan approved 2026-10-04: every unit test writes enough descriptive console output that an AI reading a failed or slow test's log can debug it without re-running it; slow budget 3 seconds per test. One task per test project; this one is `Curl.Protocol.Ssh.UnitTests`, which tests `Curl.Protocol.Ssh.UnitLibrary`.
- BL-1457 links the root `TestDiagnostics.cs` into every test project and writes each test's `START`, `END ... (arrange <a>, act <b>, assert <c>)` and `SLOW:` lines itself. The line format (`ARRANGE`, `ACT`, `ASSERT`, `BYTES`, `DIFF`, `PHASE`) and how a test calls the helper are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR; follow them and add no prefix of your own.
- Size, counted 2026-10-04 by matching `[TestMethod]`, `[TestMethod(` and `[DataTestMethod]` in the project's `.cs` files: 811 test methods in 94 files, with 1114 `[DataRow(` lines. Solution-wide, 21 test files reference `TestContext` (mostly for its `CancellationToken`) and only 3, all in `Curl.Console.UnitTests`, write any output today.
- What matters here: the packets exchanged (through `Fakes/InMemoryDuplexConnection.cs`) as `BYTES` with their decoded message number, the negotiated algorithms, key-exchange values and keys as hex, host-key and user-authentication steps (`InMemoryKeyFileSystem` key files, `FakePageantWindow` agent replies), SFTP and SCP requests and replies, and `PHASE` lines for version exchange, key exchange, authentication and channel transfer.
- Output only. No test method, data row, assertion or arrange step is removed, weakened or changed in what it tests. A shared fake or helper in this project may write the lines for the tests that use it, as long as each test's `END` line counts them.
- Keep the log readable: large payloads go through `BYTES` (which caps itself), never a loop printing thousands of lines. Nothing printed may make a test depend on the operating system.

## Acceptance criteria

- [x] `dotnet test Curl.Protocol.Ssh.UnitTests --filter "TestCategory!=Integration" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs (as many `END` lines as the run's total test count), and piping that output to `Select-String -Pattern 'END .*(\(arrange 0,|, act 0,|, assert 0\))'` prints nothing: every test wrote at least one `ARRANGE`, one `ACT` and one `ASSERT` or `DIFF` line.
- [x] The run's total test count is unchanged, and in `Curl.Protocol.Ssh.UnitTests` (excluding `obj`) the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches (each counted with `Select-String -AllMatches`) are no lower than before the task; the before and after numbers are recorded in Notes.
- [x] `dotnet build Curl.Protocol.Ssh.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.Ssh.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Protocol.Ssh.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; for each that is a real performance problem a follow-up task is filed and its ID is in Notes.

## Notes

- Large: 811 test methods in 94 files is probably more than one `/task-run` can finish. Stewart asked for one task per project, so it is filed whole. If the runner judges it too big, it splits it before changing any test: it files tasks that each cover a range of the project's files by name (each `-Pipeline direct -DependsOn BL-1457 -Touches Curl.Protocol.Ssh.UnitTests`, with these criteria limited to its files' classes through `--filter "FullyQualifiedName~<class>"`), adds them to this task's `depends-on`, and moves this task back to `Backlog`; this task then only runs the whole-project checks above.
- 2026-10-07 (lane 5): Split before changing any test, as the note above allows: 811 test methods in 94 files cannot be finished in one 120-minute run. Filed BL-1622 (SshUserAuthenticationTests*, 103), BL-1623 (other Authentication, Compression, Connection, 66), BL-1624 (HostKeys, KeyExchange, Negotiation, 80), BL-1625 (Keys, 85), BL-1626 (PacketProtection and root helper tests, 67), BL-1627 (Scp, SFTP download and listing, 104), BL-1628 (SFTP upload, quote, session, 90), BL-1629 (SshProtocolHandlerTests*, 139), BL-1630 (Transport, 102). All touch only Curl.Protocol.Ssh.UnitTests, so the board runs them one at a time. Once they are Done, this task only runs the whole-project checks.
- 2026-10-07 (lane 3): BL-1622..BL-1630 all Done; whole-project checks run.
  - Detailed run: 1673 tests, 1673 passed, 1673 `END` lines; the `arrange 0`/`act 0`/`assert 0` pattern matches nothing.
  - Counts (git grep over the project's `.cs` files), before at 88e4e1d47 (last commit before BL-1622) -> after: `Assert.` 1408 -> 1418, `[TestMethod` 811 -> 811, `[DataRow(` 1114 -> 1114. Test count 1673 before and after (BL-1622..1630 added none).
  - One change here: `SshProtocolHandlerTests.DiagnosticLog.cs`'s `RunLoggingAsync` now wraps the transfer and session end in `PHASE transfer` / `PHASE session end`, as `SshProtocolHandlerTests.cs` already does, because `ExecuteAsync_EncryptedKeyAtVerbose_NoMessageContainsThePassPhraseOrAPrivateKeyByte` printed `SLOW:` with no breakdown. Output only.
  - SLOW lines (two runs, machine loaded by other lanes; slowest instance shown):
    - Encrypted OpenSSH keys, all dominated by bcrypt_pbkdf: `SshPrivateKeyReaderTests.Read_RsaOpenSshEncrypted_ReadsAnRsaKey` 49132 ms (read with the passphrase 26229, wrong passphrase 22902); `Read_Ed25519KeyEncryptedWithEachCipher_ReadsTheKeyOfItsPublicKeyFile` up to 25771 ms on every row (read and decrypt 25771); `Read_EncryptedOpenSshKeyWithoutOrWithAWrongPassphrase_ReadsNone` 25386 (without 0, wrong passphrase 25385); `Read_EcdsaOpenSshEncryptedWithAes256Gcm_ReadsTheKeyOfItsPublicKeyFile` 24261 (read and decrypt 24260); `OpenSshPrivateKeyDecoderTests.Read_WrongPassphrase_ThrowsAsTheCheckIntegersDiffer` 23099 (bcrypt-pbkdf and decrypt 23098); `Read_GcmWrongPassphrase_ThrowsAsTheTagFails` 19249 (bcrypt-pbkdf and decrypt 19248); `SshUserAuthenticationTests.AuthenticateAsync_EncryptedEd25519KeyWithItsPassphrase_AsksThenSignsWithSshEd25519` 37744 (key exchange 3, authentication 37690); `..._EncryptedKeyNotOpenedOnWinCng_ReportsReasonUnknownAsMeasured` 19780 (key exchange 9, authentication 19753); `..._EncryptedKeyNotOpenedOnOpenSsl_ReportsTheUnrecognizedKeyFileAsMeasured` 19097 (4, 19074); `..._EncryptedOpenSshKeyNotOpened_SendsNoPublicKeyRequestAsMeasured` 18973 (4, 18948); `SshProtocolHandlerTests.ExecuteAsync_EncryptedKeyAtVerbose_NoMessageContainsThePassPhraseOrAPrivateKeyByte` 41579 (transfer 41576, session end 0); `ExecuteAsync_EncryptedEd25519KeyTheServerAuthorizes_AuthenticatesWithPublickey` 34266 (transfer 34263, session end 0); `ExecuteAsync_EncryptedKeyWithAWrongPassphrase_IsExit67AsMeasured` 21618 (transfer 21459, session end 0). A real performance problem: `Curl.Cryptography.UnitLibrary`'s `BcryptPbkdf` takes about 500 ms per bcrypt_hash. Its follow-up already exists: **BL-1558** (filed by BL-1535), so no new task was filed.
    - Load-dependent, just over the 3 s budget in one run only: `SshTransportTests.ExchangeKeysAsync_GroupExchangeOnEachPreset_AsksForThePresetsSizesAndAcceptsAPrimeAtItsMaximum` 3783 (script the server 2101, key exchange 1681; 8192-bit prime work, already judged not a problem by BL-1630); `SshTransportTests.ExchangeKeysAsync_EachMethod_ReachesTheServersExchangeHashAndKeys` 4243 (key exchange rows 427..1533); `SftpFileDownloadTests.DownloadAsync_FileLargerThanOneChannelWindow_ReadsItAllAndGrowsTheWindow` 3243 (download 1481 and 1728); `SshProtocolHandlerTests.ExecuteAsync_ServerOffersOnlyALegacyCipherOrMac_TransfersOverIt` 3962 (transfer 1134..3957, session end 0). Not filed: each passed under budget in the other run, and the time is CPU contention from other lanes' builds, not a hot spot in the code.

## Log

- 2026-10-04: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Backlog. Split into BL-1622..BL-1630 (one per folder group); waits on them, then runs only the whole-project checks.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Whole-project checks pass: 1673 END lines, none with a zero count; counts not lower; SLOW causes covered by BL-1558.
