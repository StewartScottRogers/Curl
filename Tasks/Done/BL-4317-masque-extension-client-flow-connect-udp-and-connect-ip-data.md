---
id: BL-4317
title: MASQUE extension client flow: CONNECT-UDP and CONNECT-IP datagram connectors over curl's connector
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4319]
touches: [Curl.Extension.Masque.UnitLibrary/Tunnel, Curl.Extension.Masque.UnitTests/Tunnel, Curl.Extension.Masque.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4317 â€” MASQUE extension client flow: CONNECT-UDP and CONNECT-IP datagram connectors over curl's connector

## Goal

The MASQUE extension has an `IDatagramConnector` that opens a CONNECT-UDP or CONNECT-IP tunnel to a proxy through curl's `IConnector` and carries datagrams in it as an `IDatagramChannel`.

## Context

Second of BL-4144's split, on BL-4319's wire format. `OpenAsync(host, port)` connects to the proxy the URI template names through the `IConnector` the hook is given (TLS for an `https` template), sends BL-4319's upgrade request, and on `101` returns a channel: CONNECT-UDP carries each datagram as one DATAGRAM capsule with context ID 0 (RFC 9298 section 5); CONNECT-IP sends ADDRESS_REQUEST, waits for ADDRESS_ASSIGN, and wraps each datagram in an IP and UDP packet from the assigned address to the target (RFC 9484 sections 4.7 and 6). See `Curl.Networking`'s `CapsuleDatagramChannel` (ADR-0726) for how curl's own `-x` tunnel behaves, without referencing it. Failures map to curl exit codes: a proxy that cannot be reached keeps the connector's code, a refused upgrade is exit 7 with the status, a tunnel the proxy closes fails receive as a reset socket.

## Acceptance criteria

- [x] Over a fake connector, a CONNECT-UDP channel sends and receives datagrams as RFC 9298 section 5 DATAGRAM capsules, and drops capsules of unknown type (RFC 9297 section 3.2); unit tests.
- [x] Over a fake connector, a CONNECT-IP channel requests and uses its assigned address, sends IP/UDP packets to the target and hands back only the UDP payloads from it (RFC 9484 sections 4.7 and 6); unit tests.
- [x] A refused upgrade, an unreachable proxy and a tunnel closed by the proxy each fail with the exit code and message the Context names; unit tests.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Plan: `Tunnel/MasqueDatagramConnector` (the `IDatagramConnector`) reaches the proxy through the given `IConnector` (TLS for an `https` template), writes `MasqueUpgrade`'s request and checks the reply; `MasqueTunnelStream` reads the head and capsules over the `IConnection`; `ConnectUdpChannel` and `ConnectIpChannel` are the two `IDatagramChannel`s. Decisions in ADR-1116 (Decided by Claude under Stewart's delegation).
- Pipeline `feature` was run directly by the lane (one library, the plan above), to keep inside the run's cost cap; the ADR records the design.
- Defaults taken: CONNECT-IP needs an IP address literal target (the packets carry it and the extension has no resolver), a host name is exit 6; ADDRESS_REQUEST ID 1 for the unspecified address, the first assigned address of the target's family used; UDP source port random in 49152-65535; a datagram longer than the receive buffer is cut to fit; at most 1 MiB buffered waiting for a head or capsule (exit 7 past it).
- Failures: unreachable proxy keeps the connector's code and message; refused upgrade exit 7 `MASQUE proxy refused the connect-udp upgrade with status 403`; proxy closing before the reply or before ADDRESS_ASSIGN exit 7; a tunnel closed later fails receive with `SocketError.ConnectionReset`.
- Added `Curl.Extension.Masque.UnitLibrary/CLAUDE.md` to touches: it said the library held the wire format only, which this task made untrue. No task in Doing on origin/work/dark-factory names it.
- An `await` inside `catch` leaves a compiler-generated rethrow line coverage cannot reach; the connector captures the exception, disposes the connection and throws it outside the catch instead.
- Measured: Measure-CodeQuality.ps1 -Library Curl.Extension.Masque.UnitLibrary: 100% line, 100% branch, max complexity 10, no failing member. Full `dotnet build` clean; fast tests green (Masque 102 tests). Tests are platform-neutral (no paths, OS error text or certificates).

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 826d30f6 6fa7d036 c9e51c58 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. MASQUE extension opens CONNECT-UDP and CONNECT-IP datagram channels through curl's connector
