---
id: BL-4095
title: Add connection hooks for extensions: custom transports under a transfer
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4092]
touches: [Curl.Extension.Abstractions.UnitLibrary/Connection, Curl.Extension.Abstractions.UnitTests/Connection, Curl.Engine.UnitLibrary/EngineComposition.cs, Curl.Engine.UnitTests/EngineCompositionTests.Extensions.cs]
requirement: none
created: 2026-10-29
completed:
---
# BL-4095 — Add connection hooks for extensions: custom transports under a transfer

## Goal

An extension activated by its option can supply the `IConnector` (and `IDatagramConnector`) a transfer uses, so a transfer can run over a named pipe, vsock, an SSH jump host or a tunnel.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Needed by named pipes, vsock, MASQUE, SSH jump hosts and WireGuard. Mirrors how `--unix-socket` replaces the connector today.

## Acceptance criteria

- [ ] A test extension's connector carries one fake HTTP transfer end to end (engine unit test, no network).
- [ ] Two extensions that both claim the connector for one transfer fail with a clear message naming both options (unit test).
- [ ] Without an extension option the recording connector curl uses is unchanged (unit test).
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes



## Log

- 2026-10-29: Created.
