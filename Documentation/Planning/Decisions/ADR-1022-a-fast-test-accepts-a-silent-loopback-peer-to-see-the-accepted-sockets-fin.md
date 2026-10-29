# ADR-1022: A fast test accepts a silent loopback peer to see the accepted socket's FIN

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4181)

## Context

Audit finding AF-0458: `TcpPendingConnection.AcceptStreamConnectionAsync` wraps the accepted socket in
`new NetworkStream(accepted, ownsSocket: true)`, and turning that to `false` left every
`Curl.Networking.UnitTests` test passing. With `false`, disposing the accepted `StreamConnection` leaves
the socket open, so the peer of an active-mode (PORT/EPRT) FTP data connection never sees a FIN and an
upload's end is never signalled. The method is excluded from coverage (ADR-0083) and only the
Integration run reaches it, which the mutation run does not run.

`NetworkStream` refuses an unconnected socket, so no seam lets a test observe the ownership without a
connected peer. ADR-0421 counts a loopback socket that sends or receives no byte as a unit test, and a
loopback round trip as an Integration test.

## Decision

`TcpPendingConnectionTests.AcceptAsync_AcceptedConnectionDisposed_ClosesTheAcceptedSocket` is a fast
unit test in `Curl.Networking.UnitTests`: a loopback peer connects to a `TcpPendingConnection` and sends
nothing. The test accepts through the production accept step, disposes the accepted connection, and
expects the peer's receive to return 0 (the FIN) within ten seconds.

No payload byte crosses the socket, only the TCP handshake and the FIN. This is the same precedent as
`DeferredConnectSocketStreamTests`' and `TcpDialerTests`' loopback connects in the fast run, so the test
stays within ADR-0421's unit-test rule. The production code is unchanged, and the method keeps its
`[ExcludeFromCodeCoverage]` because its failure branch still needs the Integration run.

## Why

The fast tests are what the mutation run and the coverage gate run, so only a fast test can kill the
mutant. Moving the check to `Curl.Networking.IntegrationTests` would leave AF-0458 reproducing. A peer
that sends nothing is deterministic and platform-neutral: the test waits for the FIN event and does not
rely on timing.
