---
id: BL-1549
title: Make TcpConnectorTests' Socks to WithoutConnectTimeout files write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457, BL-1548]
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1549 — Make TcpConnectorTests' Socks to WithoutConnectTimeout files write descriptive diagnostic output

## Goal

Every test declared in `Curl.Networking.UnitTests`' `TcpConnectorTests` partial files `.Socks`, `.Socks5Authentication`, `.SocksFilterTrace`, `.SslFilterTrace`, `.Timings`, `.TlsRecordTrace`, `.UnixSocket` and `.WithoutConnectTimeout` (8 files, 120 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings for resolve, connect, proxy handshake and TLS handshake), with no test's logic or assertions changed; with BL-1546 to BL-1548 Done, the whole class then does.

## Context

- Split from BL-1468 (one task per range of files, as its Notes direct); BL-1468 keeps the whole-project checks and depends on this task. Follow BL-1468's Context: what matters in this project, `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- `TcpConnectorTests` is one partial class of 450 tests across 47 files, split in name order into BL-1546, BL-1547, BL-1548 and this task, each depending on the one before, so this task's check covers the whole class.

## Acceptance criteria

- [x] `dotnet test Curl.Networking.UnitTests --filter "FullyQualifiedName~Curl.Networking.TcpConnectorTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Networking.UnitTests -warnaserror` is clean and `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Networking.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Connects in the 8 files go through `ConnectLoggedAsync` (ARRANGE target, a `connect` PHASE, ACT exit code and error message). `ConnectThroughSocksAsync` and `TraceThroughSocksAsync` became instance methods for it; the first also writes the proxy, its reply and the bytes sent (`BYTES`), the second the info lines. `AssertProxyFailure` writes ASSERT lines for exit code and message.
- Every test then writes at least one ASSERT or DIFF line before its first assertion: exit codes, bytes sent to the proxy (`DIFF` for single-line byte comparisons, counts otherwise), trace line counts and contents, timings. The seven tests that never connect through the logger (`Socks5Authentication_WhenNoneWasGiven`, the three `TlsRecordTraceFor`/`HttpsLines` tests, `UnixSocket_IsTheConstructorsSocket` and two `WithoutConnectTimeout` tests) write their own ARRANGE and ACT.
- Choice: line lists compared with `CollectionAssert` get a count or a single-line ASSERT rather than a duplicated expected array, to leave every assertion untouched; `SLOW:` budget unaffected.
- Counts in the 8 files, before -> after: `Assert.` 217 -> 217, `[TestMethod` 120 -> 120, `[DataRow(` 76 -> 76.
- Class check: `dotnet test ... --filter FullyQualifiedName~Curl.Networking.TcpConnectorTests.` printed 545 END lines, none with arrange, act or assert 0. No test printed a `SLOW:` line, so no follow-up task.
- `dotnet build Curl.Networking.UnitTests -warnaserror`: 0 warnings, 0 errors; fast tests 3032 passed, 29 skipped; `dotnet format --verify-no-changes` clean. Only a test project changed, so no library coverage was measured.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every TcpConnectorTests test from Socks to WithoutConnectTimeout writes ARRANGE, ACT, ASSERT and a connect PHASE; the whole class prints no zero count
