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
completed:
---
# BL-1549 — Make TcpConnectorTests' Socks to WithoutConnectTimeout files write descriptive diagnostic output

## Goal

Every test declared in `Curl.Networking.UnitTests`' `TcpConnectorTests` partial files `.Socks`, `.Socks5Authentication`, `.SocksFilterTrace`, `.SslFilterTrace`, `.Timings`, `.TlsRecordTrace`, `.UnixSocket` and `.WithoutConnectTimeout` (8 files, 120 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings for resolve, connect, proxy handshake and TLS handshake), with no test's logic or assertions changed; with BL-1546 to BL-1548 Done, the whole class then does.

## Context

- Split from BL-1468 (one task per range of files, as its Notes direct); BL-1468 keeps the whole-project checks and depends on this task. Follow BL-1468's Context: what matters in this project, `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- `TcpConnectorTests` is one partial class of 450 tests across 47 files, split in name order into BL-1546, BL-1547, BL-1548 and this task, each depending on the one before, so this task's check covers the whole class.

## Acceptance criteria

- [ ] `dotnet test Curl.Networking.UnitTests --filter "FullyQualifiedName~Curl.Networking.TcpConnectorTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [ ] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [ ] `dotnet build Curl.Networking.UnitTests -warnaserror` is clean and `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` passes.
- [ ] The task's commits change only files under `Curl.Networking.UnitTests/` and this task file.
- [ ] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
