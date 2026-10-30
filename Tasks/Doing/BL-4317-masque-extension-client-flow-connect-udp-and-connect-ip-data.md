---
id: BL-4317
title: MASQUE extension client flow: CONNECT-UDP and CONNECT-IP datagram connectors over curl's connector
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4319]
touches: [Curl.Extension.Masque.UnitLibrary/Tunnel, Curl.Extension.Masque.UnitTests/Tunnel]
requirement: none
created: 2026-10-29
completed:
---
# BL-4317 — MASQUE extension client flow: CONNECT-UDP and CONNECT-IP datagram connectors over curl's connector

## Goal

The MASQUE extension has an `IDatagramConnector` that opens a CONNECT-UDP or CONNECT-IP tunnel to a proxy through curl's `IConnector` and carries datagrams in it as an `IDatagramChannel`.

## Context

Second of BL-4144's split, on BL-4319's wire format. `OpenAsync(host, port)` connects to the proxy the URI template names through the `IConnector` the hook is given (TLS for an `https` template), sends BL-4319's upgrade request, and on `101` returns a channel: CONNECT-UDP carries each datagram as one DATAGRAM capsule with context ID 0 (RFC 9298 section 5); CONNECT-IP sends ADDRESS_REQUEST, waits for ADDRESS_ASSIGN, and wraps each datagram in an IP and UDP packet from the assigned address to the target (RFC 9484 sections 4.7 and 6). See `Curl.Networking`'s `CapsuleDatagramChannel` (ADR-0726) for how curl's own `-x` tunnel behaves, without referencing it. Failures map to curl exit codes: a proxy that cannot be reached keeps the connector's code, a refused upgrade is exit 7 with the status, a tunnel the proxy closes fails receive as a reset socket.

## Acceptance criteria

- [ ] Over a fake connector, a CONNECT-UDP channel sends and receives datagrams as RFC 9298 section 5 DATAGRAM capsules, and drops capsules of unknown type (RFC 9297 section 3.2); unit tests.
- [ ] Over a fake connector, a CONNECT-IP channel requests and uses its assigned address, sends IP/UDP packets to the target and hands back only the UDP payloads from it (RFC 9484 sections 4.7 and 6); unit tests.
- [ ] A refused upgrade, an unreachable proxy and a tunnel closed by the proxy each fail with the exit code and message the Context names; unit tests.
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 826d30f6 6fa7d036 c9e51c58 e35aff77 3d836d5e.
