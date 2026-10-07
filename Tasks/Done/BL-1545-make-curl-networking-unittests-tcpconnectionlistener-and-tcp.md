---
id: BL-1545
title: Make Curl.Networking.UnitTests' TcpConnectionListener and TcpConnectorQuic tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1545 — Make Curl.Networking.UnitTests' TcpConnectionListener and TcpConnectorQuic tests write descriptive diagnostic output

## Goal

Every test in `Curl.Networking.UnitTests`' `TcpConnectionListenerTests.cs` and the partial class `TcpConnectorQuicTests` (`TcpConnectorQuicTests.cs` and its 10 `TcpConnectorQuicTests.*.cs` files: 12 files, 120 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings for resolve, connect and handshake), with no test's logic or assertions changed.

## Context

- Split from BL-1468 (one task per range of files, as its Notes direct); BL-1468 keeps the whole-project checks and depends on this task. Follow BL-1468's Context: what matters in this project, `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.

## Acceptance criteria

- [x] `dotnet test Curl.Networking.UnitTests --filter "FullyQualifiedName~Curl.Networking.TcpConnectionListener|FullyQualifiedName~Curl.Networking.TcpConnectorQuicTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Networking.UnitTests -warnaserror` is clean and `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Networking.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Approach: `TcpConnectorQuicTests` gained `TestContext`, `Diagnostics` and helpers in
  `TcpConnectorQuicTests.cs` - `ConnectMultiplexedAsync(connector, target)` and
  `ConnectAsync(connector, target)` write the target as ARRANGE, time the call as
  `PHASE resolve, connect and handshake`, and write the exit code and error message as ACT;
  `ActEvents`, `ActFilterLines`, `ActTunnelTranscript`, `ActBoundFrom` and `ActCipherSuites`
  write the lines each file asserts on. `TcpConnectionListenerTests` has a `ListenAsync` helper
  with `PHASE bind and listen`. Each test adds its own ARRANGE and an ASSERT line for its first
  assertion; no assertion was changed, removed or reordered.
- Nothing printed depends on the operating system (BL-1468 Context): where an error message holds
  the platform's own words (errno and system message on a receive failure, Schannel certificate
  and CA-file text, the temporary path, a socket exception's message), the helper is called with
  `writesErrorMessage: false` and the test writes only an OS-neutral fact about it. The two
  port-range tests that go inconclusive when another process holds the next port write their
  ACT and ASSERT before `Assert.Inconclusive`, so their counts are non-zero either way.
- Counts across the 12 files, before -> after: `Assert.` 308 -> 308, `[TestMethod` 119 -> 119,
  `[DataRow(` 10 -> 10 (`Diagnostics.Assert(` does not match `Assert\.`). The filter runs 125
  tests with DataRows expanded: 124 passed, 1 skipped (Linux only), 124 `END` lines, none with a
  zero count.
- SLOW: none. The longest PHASE was `resolve, connect and handshake: 303 ms`.
- Full fast run of the project: 3033 passed, 28 skipped, 0 failed. No library changed, so
  Measure-CodeQuality was not run (test-only task).

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. TcpConnectionListener and TcpConnectorQuic tests write ARRANGE, ACT, ASSERT and PHASE diagnostics with no OS-specific text
