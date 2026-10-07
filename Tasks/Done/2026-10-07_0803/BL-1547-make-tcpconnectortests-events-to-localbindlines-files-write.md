---
id: BL-1547
title: Make TcpConnectorTests' Events to LocalBindLines files write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457, BL-1546]
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1547 — Make TcpConnectorTests' Events to LocalBindLines files write descriptive diagnostic output

## Goal

Every test declared in `Curl.Networking.UnitTests`' `TcpConnectorTests` partial files `.Events`, `.FailureReason`, `.HappyEyeballs`, `.HaproxyFilterTrace`, `.HaproxyProtocol`, `.Http2ProxyTunnel`, `.HttpProxyTunnelTrace`, `.HttpsConnectTrace`, `.HttpsConnectTraceRoutes`, `.HttpsProxy`, `.HttpsProxyTunnelTrace`, `.LiteralTimings`, `.LocalBinding` and `.LocalBindLines` (14 files, 117 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings for resolve, connect, proxy handshake and TLS handshake), with no test's logic or assertions changed.

## Context

- Split from BL-1468 (one task per range of files, as its Notes direct); BL-1468 keeps the whole-project checks and depends on this task. Follow BL-1468's Context: what matters in this project, `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- `TcpConnectorTests` is one partial class of 450 tests across 47 files, split in name order into BL-1546, BL-1547, BL-1548 and BL-1549, each depending on the one before; BL-1549 checks the whole class.

## Acceptance criteria

- [x] `dotnet test Curl.Networking.UnitTests --filter "FullyQualifiedName~Curl.Networking.TcpConnectorTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and no `END` line for a test declared in this task's 14 files matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Networking.UnitTests -warnaserror` is clean and `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Networking.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Approach: as BL-1546 - connects of `TcpConnector` with `CancellationToken.None` go through `ConnectLoggedAsync` (ARRANGE target, `connect` PHASE, ACT exit code and error message), and each test writes an ASSERT mirroring its first assertion. Tests that act otherwise write their own ARRANGE and ACT: the Happy Eyeballs tests that hold the connect task and drive time (ARRANGE timeout, ACT connected), the caller-cancellation test, the h2-proxy EndOfStream test, `HaproxyProtocol_IsTheHeaderGiven` and the SOCKS route (a `socks handshake` PHASE). `TcpConnector` exposes no separate resolve, proxy or TLS seam, so `connect` covers them all, as in BL-1546.
- Helpers `ConnectThroughExplainedSocksAsync`, `TraceThroughTunnelAsync`, `AssertTunnelLines`, `TraceThroughHttpsProxyAsync` and LocalBindLines' four `...LinesAsync` helpers became instance methods so they can write diagnostics. `AssertTunnelLines` also writes a DIFF of the lines joined with `\n` (not `Environment.NewLine`, so the output does not depend on the operating system).
- Where the first assertion compares a long line array, the ASSERT line checks its count or one line rather than repeating the array; the real assertion is unchanged.
- Counts, before -> after (`Assert.` / `[TestMethod` / `[DataRow(`), all unchanged: Events 33/11/2; FailureReason 13/6/4; HappyEyeballs 30/11/5; HaproxyFilterTrace 12/8/2; HaproxyProtocol 10/7/0; Http2ProxyTunnel 23/5/2; HttpProxyTunnelTrace 15/12/2; HttpsConnectTrace 11/7/4; HttpsConnectTraceRoutes 7/7/0; HttpsProxy 50/9/7; HttpsProxyTunnelTrace 11/7/0; LiteralTimings 3/1/0; LocalBinding 39/16/10; LocalBindLines 14/10/4.
- The filtered detailed run printed 135 END lines for the 117 methods here (data rows included), none with a zero count. Five methods are off-Windows only (`OSCondition`) and were skipped on this Windows lane, so printed no END line.
- No test printed a `SLOW:` line.
- `dotnet format --verify-no-changes` still reports end-of-line errors in files outside this task (e.g. `HandBuiltTlsProviderTests.CertificateStatus.cs`); none in this task's 14 files.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every test in the 14 TcpConnectorTests files from Events to LocalBindLines writes ARRANGE, ACT and ASSERT lines and a connect PHASE; build clean, Curl.Networking.UnitTests green
