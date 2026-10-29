---
id: BL-4095
title: Add connection hooks for extensions: custom transports under a transfer
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4092]
touches: [Curl.Extension.Abstractions.UnitLibrary/Connection, Curl.Extension.Abstractions.UnitLibrary/CLAUDE.md, Curl.Extension.Abstractions.UnitTests/Connection, Curl.Engine.UnitLibrary/EngineComposition.cs, Curl.Engine.UnitTests/EngineCompositionTests.Extensions.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4095 — Add connection hooks for extensions: custom transports under a transfer

## Goal

An extension activated by its option can supply the `IConnector` (and `IDatagramConnector`) a transfer uses, so a transfer can run over a named pipe, vsock, an SSH jump host or a tunnel.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Needed by named pipes, vsock, MASQUE, SSH jump hosts and WireGuard. Mirrors how `--unix-socket` replaces the connector today.

## Acceptance criteria

- [x] A test extension's connector carries one fake HTTP transfer end to end (engine unit test, no network).
- [x] Two extensions that both claim the connector for one transfer fail with a clear message naming both options (unit test).
- [x] Without an extension option the recording connector curl uses is unchanged (unit test).
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Plan: `IConnectionHook` and `ConnectionHooks` in `Curl.Extension.Abstractions/Connection`; the engine takes `extensionConnector` and `extensionDatagramConnector` on `CreateProtocolHandlers` and applies them to its `connector` and `datagramConnector` before anything else, as `--unix-socket` replaces the connector a host passes (ADR-1010).
- Decided: one hook per transfer, not a chain; two activated hooks throw an `ArgumentException` naming every option, which the host reports as an option error. A hook receives curl's connector so a jump host or tunnel can reach its first hop through it. Datagram connector defaults to curl's.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Extension.Abstractions.UnitLibrary,Curl.Engine.UnitLibrary` reported 0 failing members.
- Added `Curl.Extension.Abstractions.UnitLibrary/CLAUDE.md` to touches so the project document names the connection hooks; no other task in Doing names it.
- Filed BL-4192 for the Console wiring (passing the activated hook from the parsed extension options), mirroring BL-4173 for transfer hooks.



## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 82be7147 1838f366 56448fe1 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. Extensions can supply the connector and datagram connector a transfer runs over through IConnectionHook; two claiming one transfer fail naming both options
