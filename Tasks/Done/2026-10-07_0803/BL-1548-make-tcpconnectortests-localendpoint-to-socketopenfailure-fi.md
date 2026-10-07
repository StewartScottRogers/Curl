---
id: BL-1548
title: Make TcpConnectorTests' LocalEndPoint to SocketOpenFailure files write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457, BL-1547]
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1548 — Make TcpConnectorTests' LocalEndPoint to SocketOpenFailure files write descriptive diagnostic output

## Goal

Every test declared in `Curl.Networking.UnitTests`' `TcpConnectorTests` partial files `.LocalEndPoint`, `.LocalInterfaceLine`, `.Onion`, `.Overrides`, `.PreProxy`, `.Proxy`, `.ProxyAuth`, `.ProxyAuthVerbose`, `.ProxyDigestStale`, `.ProxyNtlmAndNegotiate`, `.SetupFilterTrace`, `.SharedDnsCache` and `.SocketOpenFailure` (13 files, 97 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings for resolve, connect, proxy handshake and TLS handshake), with no test's logic or assertions changed.

## Context

- Split from BL-1468 (one task per range of files, as its Notes direct); BL-1468 keeps the whole-project checks and depends on this task. Follow BL-1468's Context: what matters in this project, `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- `TcpConnectorTests` is one partial class of 450 tests across 47 files, split in name order into BL-1546, BL-1547, BL-1548 and BL-1549, each depending on the one before; BL-1549 checks the whole class.

## Acceptance criteria

- [x] `dotnet test Curl.Networking.UnitTests --filter "FullyQualifiedName~Curl.Networking.TcpConnectorTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and no `END` line for a test declared in this task's 13 files matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Networking.UnitTests -warnaserror` is clean and `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Networking.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Approach: as BL-1546 and BL-1547 - connects of `TcpConnector` with `CancellationToken.None` go through `ConnectLoggedAsync` (ARRANGE target, `connect` PHASE, ACT exit code and error message), and each test writes an ASSERT mirroring its first assertion (exit code where it has one). `TcpConnector` exposes no separate resolve, proxy or TLS seam, so `connect` covers them all. The two Integration tests in `.LocalEndPoint` connect with their own token, so they write their own ARRANGE, PHASE (`connect`, and `connect and TLS handshake` for the TLS one) and ACT.
- `AssertTranscript` (ProxyAuthVerbose) became an instance method and writes a DIFF of the transcripts joined with `\n` before its assertion; `ConnectRefusedNegotiateTunnelAsync` became an instance method so it can connect through `ConnectLoggedAsync`. `.LocalInterfaceLine` tests get ARRANGE and ACT from `DeviceBindLinesAsync`, already logged by BL-1547.
- OS-neutral output: `.SocketOpenFailure` asserts the exit code rather than the platform's socket-open lines, and the `SocketOpenFailedLines` tests write the line count, not the system's error text. The `SocketOpenFailedLines` tests now assert on the `lines` they computed instead of calling the same pure method a second time; same values, same assertions.
- Where the first assertion compares a long line array (SetupFilterTrace, SharedDnsCache), the ASSERT line checks the count or the first line; the real assertion is unchanged.
- Counts, before -> after (`Assert.` / `[TestMethod` / `[DataRow(`), all unchanged: LocalEndPoint 7/2/0; LocalInterfaceLine 6/5/2; Onion 14/4/6; Overrides 33/13/0; PreProxy 34/12/4; Proxy 48/14/5; ProxyAuth 53/11/4; ProxyAuthVerbose 4/10/7; ProxyDigestStale 21/4/0; ProxyNtlmAndNegotiate 40/10/0; SetupFilterTrace 4/3/0; SharedDnsCache 10/5/2; SocketOpenFailure 10/4/0.
- The filtered detailed run printed 547 END lines (every test run), 113 of them for 95 of this task's 97 methods (data rows included), none with a zero count. The other two are `OSCondition` tests skipped on this Windows lane (`...ToIPv6OnLinux_ReportsAddressFamily10`, `...WhenTheContextHasNoCredentials_FailsWithThe407`). Zero-count END lines remain for Socks tests, which are BL-1549's range.
- No test printed a `SLOW:` line.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. TcpConnectorTests' LocalEndPoint to SocketOpenFailure tests write ARRANGE, ACT, ASSERT and connect PHASE diagnostics
