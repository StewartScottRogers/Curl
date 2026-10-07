---
id: BL-1556
title: Make Curl.Networking.UnitTests' other Ha and Http tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1556 — Make Curl.Networking.UnitTests' other Ha and Http tests write descriptive diagnostic output

## Goal

Every test in `Curl.Networking.UnitTests`' `Ha*` and `Http*` test files other than `HandBuiltTlsProviderTests*.cs` (`HandBuiltCertificateVerifierTests`, `HandBuiltHandshakeTests`, `HandBuiltPrivateKeyReaderTests`, `HandBuiltTlsConnectionClearTlsTests`, `HandshakeCapturingTransferEventsTests`, `HaproxyProtocolHeaderTests`, `Http2ProxyTunnelConnectionTests`, `HttpApplicationProtocolsTests`, `HttpProxyTunnelTests`, `HttpsConnectFilterTraceEventsTests`: 10 files, 109 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed.

## Context

- Split from BL-1468 (one task per range of files, as its Notes direct); BL-1468 keeps the whole-project checks and depends on this task. Follow BL-1468's Context: what matters in this project, `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.

## Acceptance criteria

- [x] `dotnet test Curl.Networking.UnitTests --filter "(FullyQualifiedName~Curl.Networking.Ha&FullyQualifiedName!~Curl.Networking.HandBuiltTlsProviderTests.)|FullyQualifiedName~Curl.Networking.Http" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Networking.UnitTests -warnaserror` is clean and `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Networking.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- 2026-10-07 (lane 1): all 10 files now write ARRANGE, ACT and ASSERT/DIFF lines through
  `TestDiagnostics`; expression-bodied tests became block bodies, and shared per-file
  helpers (`ArrangeEnds`/`AssertLine`, `ArrangeFilter`/`AssertCalls`, `ArrangeRequest`/
  `AssertRequest`/`ArrangeReply`/`ActReply`, `ArrangeClear`, `ActLoad`) write the common
  lines. `PHASE` wraps the TLS handshakes and clears in `HandBuiltTlsConnectionClearTlsTests`,
  the tunnel opens and window-blocked write in `Http2ProxyTunnelConnectionTests`, and the key
  loads in `HandBuiltPrivateKeyReaderTests`. Requests, replies and keys go through `BYTES`.
- Nothing printed depends on the OS: `HandBuiltPrivateKeyReaderTests` writes its temporary
  directory as `<temp>` in error messages (`WithoutDirectory`).
- Counts before -> after (the 10 files): `Assert.` 178 -> 178, `[TestMethod` 109 -> 109,
  `[DataRow(` 153 -> 153. No assertion was removed or weakened; `Diagnostics.Assert(` lines
  are additional and do not match `Assert.`.
- The filtered detailed run: 229 test results, 229 `END` lines, none with a zero count.
  Fast tests of the project: 3033 passed, 30 skipped, 0 failed.
- `SLOW:` lines: none. The slowest test ran 441 ms.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. The 10 other Ha* and Http* test files in Curl.Networking.UnitTests write ARRANGE, ACT, ASSERT, BYTES and PHASE diagnostics
