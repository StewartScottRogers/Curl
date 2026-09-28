---
id: BL-721
title: Add the datagram channel, QUIC stream and HTTP/3 version contracts to Curl.Protocol.Abstractions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-718]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-721 — Add the datagram channel, QUIC stream and HTTP/3 version contracts to Curl.Protocol.Abstractions

## Goal

`Curl.Protocol.Abstractions.UnitLibrary` gains exactly the contracts BL-718's ADR names for HTTP/3: a datagram channel (send and receive UDP datagrams, local and remote endpoints), a multiplexed connection that opens bidirectional streams and accepts unidirectional ones (each a byte stream with its own end and reset), the connector method that yields one, and the `HttpVersionPreference` values for `--http3` and `--http3-only`; documented, with defaults that leave every existing handler unchanged.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Contracts land before the work that uses them: QUIC (BL-724 uses the datagram channel), Networking (BL-728 implements both), HTTP (BL-731 consumes streams). Design: BL-718's ADR; add nothing it does not name.
- Files: `Curl.Protocol.Abstractions.UnitLibrary/IConnection.cs`, `IConnector.cs`, `HttpVersionPreference.cs` and whatever new files the ADR lists. `Abstractions_References_Nothing` must still pass.

## Acceptance criteria

- [ ] Each new type and member has XML docs naming its consumer; `Curl.Protocol.Abstractions.UnitTests` cover any defaults and the new enum values.
- [ ] `ProtocolIsolationTests` still pass.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
