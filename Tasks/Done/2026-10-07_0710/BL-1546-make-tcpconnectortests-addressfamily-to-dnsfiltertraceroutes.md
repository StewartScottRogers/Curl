---
id: BL-1546
title: Make TcpConnectorTests' AddressFamily to DnsFilterTraceRoutes files write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1546 — Make TcpConnectorTests' AddressFamily to DnsFilterTraceRoutes files write descriptive diagnostic output

## Goal

Every test declared in `Curl.Networking.UnitTests`' `TcpConnectorTests.cs` and its partial files `.AddressFamily`, `.AltSvc`, `.ConnectAttemptTrace`, `.ConnectReplyHead`, `.ConnectTimeout`, `.DeviceBindLine`, `.DiagnosticLog`, `.DnsCache`, `.DnsFilterTrace` and `.DnsFilterTraceRoutes` (11 files, 116 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings for resolve, connect, proxy handshake and TLS handshake), with no test's logic or assertions changed.

## Context

- Split from BL-1468 (one task per range of files, as its Notes direct); BL-1468 keeps the whole-project checks and depends on this task. Follow BL-1468's Context: what matters in this project, `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- `TcpConnectorTests` is one partial class of 450 tests across 47 files, split in name order into BL-1546, BL-1547, BL-1548 and BL-1549, each depending on the one before; BL-1549 checks the whole class.

## Acceptance criteria

- [x] `dotnet test Curl.Networking.UnitTests --filter "FullyQualifiedName~Curl.Networking.TcpConnectorTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and no `END` line for a test declared in this task's 11 files matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Networking.UnitTests -warnaserror` is clean and `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Networking.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Approach: the class gets `TestContext` and a `Diagnostics` property in `TcpConnectorTests.cs`, and a `ConnectLoggedAsync` helper that writes the target as ARRANGE, runs `ConnectAsync` inside a `connect` PHASE and writes the exit code and error message as ACT. Calls of `ConnectAsync(x, CancellationToken.None)` in these 11 files go through it; each test then writes at least one ASSERT before its first assertion, and the few tests that do not connect (ALPN list, cancellation, `LoadResolveEntries`) write their own ARRANGE and ACT. `TcpConnector` has no separate resolve or TLS seam visible to the tests, so `connect` covers resolve, dial, proxy handshake and TLS handshake together.
- `DeviceBindLinesAsync` became an instance method so it can write diagnostics; nothing else in a test's logic changed.
- Counts, before -> after (`Assert.` / `[TestMethod` / `[DataRow(`): TcpConnectorTests 60/25/9 -> 60/25/9; AddressFamily 23/8/6 -> 23/8/6; AltSvc 18/10/0 -> 22/10/0; ConnectAttemptTrace 15/11/2 -> 15/11/2; ConnectReplyHead 8/4/0 -> 8/4/0; ConnectTimeout 17/7/2 -> 17/7/2; DeviceBindLine 7/4/4 -> 7/4/4; DiagnosticLog 41/21/0 -> 41/21/0; DnsCache 30/15/0 -> 30/15/0; DnsFilterTrace 11/7/0 -> 11/7/0; DnsFilterTraceRoutes 9/4/0 -> 9/4/0. AltSvc's rise is ASSERT lines whose expressions quote `Assert.ContainsSingle`; no assertion was removed or changed.
- The filtered detailed run printed 130 END lines for the 116 tests here (data rows included), none with a zero count. The zero-count END lines it still prints belong to TcpConnectorTests files of BL-1547 to BL-1549.
- No test printed a `SLOW:` line.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every test in the 11 TcpConnectorTests files writes ARRANGE, ACT and ASSERT lines and a connect PHASE; build clean, Curl.Networking.UnitTests green
