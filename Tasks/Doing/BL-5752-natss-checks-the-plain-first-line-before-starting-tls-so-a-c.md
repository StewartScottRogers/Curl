---
id: BL-5752
title: natss checks the plain first line before starting TLS so a closed connection, -ERR or non-INFO line keeps its own exit code
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extended.Protocol.Nats.UnitLibrary/NatsSession.cs, Curl.Extended.Protocol.Nats.UnitTests/NatsTlsUpgradeTests.cs]
requirement: none
created: 2026-11-22
completed:
---
# BL-5752 — natss checks the plain first line before starting TLS so a closed connection, -ERR or non-INFO line keeps its own exit code

## Goal

A `natss://` transfer looks at the server's plain first line before starting TLS, so a server that closes at once, answers `-ERR`, or is not NATS gets the same exit code and message as on `nats://`, not a TLS handshake failure (exit 35).

## Context

Found by the shift-end review of 2026-11-22 (ADR-0710): silent-failure-hunter (Important), also raised by the type-design-analyzer.

BL-5750 added the plain-INFO-then-TLS upgrade. In `Curl.Extended.Protocol.Nats.UnitLibrary/NatsSession.cs`, `ReadInfoLineAsync` (`:63-74`) calls `UpgradeAsync` whenever an upgrade is configured, and `NatsServerInfo.Parse` only runs afterwards (`:42`). `ReadPlainInfoLineAsync` (`:82`) also turns a closed connection (`null` from the reader) into `string.Empty`, which is indistinguishable from a real line, so:
- a server that closes before `INFO` gets a TLS handshake over a closed socket (exit 35) where `nats://` gives exit 8 "did not begin with INFO";
- a `-ERR '...'` or non-NATS banner is never read; the user sees a TLS failure instead of the server's refusal or the protocol error.
Only the silent case (the 2-second timer) should go straight to the TLS handshake with nothing received.

## Acceptance criteria

- [ ] When the plain read returns a line, it is checked (and `-ERR` reported as on `nats://`) before any TLS handshake is attempted; a non-`INFO` line fails with the same exit code and message as on `nats://`.
- [ ] A connection closed before any line fails with the same exit code and message as on `nats://`, without a TLS handshake; only the silence timeout leads to a handshake with no line read.
- [ ] Tests in `Curl.Extended.Protocol.Nats.UnitTests/NatsTlsUpgradeTests.cs` cover: closed before INFO, `-ERR` as the first line, and a non-INFO first line, each asserting the exit code and that the TLS provider was never called.
- [ ] The library stays at 100% line and branch coverage.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-22: Created by the shift-end review (ADR-0710).
- 2026-11-22: Backlog -> Doing. Criteria at claim: 7e82c8a7 9bfe89ab 0316b17f b8a34be9 3d836d5e.
