---
id: BL-1623
title: Make the Curl.Protocol.Ssh.UnitTests agent, Pageant, compression and session-channel tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1623 — Make the Curl.Protocol.Ssh.UnitTests agent, Pageant, compression and session-channel tests write descriptive diagnostic output

## Goal

Every test in these files of `Curl.Protocol.Ssh.UnitTests` writes, through BL-1457's shared `TestDiagnostics` helper, the Arrange inputs that matter, its Act result and its assertion context (expected against actual, first differing byte or character), plus `PHASE` timings where it has distinct phases; no test's logic or assertions change. Files: Authentication/ except SshUserAuthenticationTests*, plus Compression/ and Connection/ (about 66 test methods, counted 2026-10-07).

## Context

- Split from BL-1484: one task for the whole project (811 test methods in 94 files) was too big for one `/task-run`. BL-1484's Context applies in full: the line format and helper usage in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR; what matters for SSH (packets as `BYTES` with their decoded message number, negotiated algorithms, keys as hex, authentication steps, SFTP and SCP requests and replies, `PHASE` lines); output only; `BYTES` for large payloads; nothing OS-dependent. BL-1481 (`Curl.Protocol.Rtsp.UnitTests/RtspDiagnostics.cs`) is a worked example.
- A shared helper in this project (for example `SshDiagnostics.cs`) may be added or extended by whichever split task gets there first; the others reuse it.
- Classes: OpenSshSignatureTypeBugTests, PageantAgentStreamTests, PageantSshAgentConnectorTests, PlatformSshAgentConnectorTests, SshAgentClientTests, SystemSshAgentConnectorTests, WindowsPageantWindowTests, SshZlibCompressorTests, SshZlibDecompressorTests, SshSessionChannelTests.

## Acceptance criteria

- [x] `dotnet test Curl.Protocol.Ssh.UnitTests --filter "TestCategory!=Integration&(FullyQualifiedName~<class>|...)" --logger "console;verbosity=detailed"` over this task's classes prints an `END` line for every test it runs, and piping that output to `Select-String -Pattern 'END .*(\(arrange 0,|, act 0,|, assert 0\))'` prints nothing.
- [x] The filtered run's test count is unchanged, and in this task's files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches (`Select-String -AllMatches`) are no lower than before; before and after numbers are recorded in Notes.
- [x] `dotnet build Curl.Protocol.Ssh.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.Ssh.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Protocol.Ssh.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Counts over the ten files, before and after (`Select-String -AllMatches` equivalent): `Assert.` 134 -> 134, `[TestMethod` 63 -> 63, `[DataRow(` 52 -> 52. The filtered fast run: 99 tests before and after (no test attribute changed), 99 `END` lines, none with a zero arrange, act or assert count. The whole project's fast run: 1673 passed.
- Reused BL-1622's `Authentication/SshAuthenticationDiagnostics.cs` rather than adding an `SshDiagnostics.cs`; its message-name table now also names the RFC 4253 key-exchange numbers 30-31 and the RFC 4254 connection numbers 80-100, so `SshSessionChannelTests`' client messages print as `90 CHANNEL_OPEN` and so on. `Fakes/ScriptedSshAgent.cs` gained an `Output` property so `SshAgentClientTests` can write the agent's scripted answer as `BYTES`. Both are inside `Curl.Protocol.Ssh.UnitTests`.
- Where a test asserted directly on a call's result (`Assert.IsNull(await ...)`), a small private helper now makes the call, writes `ACT`/`ASSERT`, and returns the same value to the unchanged assertion.
- The Windows-only Integration tests in `SystemSshAgentConnectorTests` and `WindowsPageantWindowTests` got the same lines; the fast filter does not run them.
- `PHASE` lines: `ReadAsync_WindowFallsBelowThreeQuarters_RestoresTheWholeWindow` (read 18 packets, ~107 ms) and `OpenAsync_ServerStartsAKeyReExchange_AnswersItAndCarriesOn` (first key exchange ~22 ms, open through the re-exchange ~95 ms).
- No test printed a `SLOW:` line.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. All 99 tests in the agent, Pageant, compression and session-channel classes write ARRANGE, ACT and ASSERT lines through TestDiagnostics; counts unchanged, 1673 fast tests pass.
