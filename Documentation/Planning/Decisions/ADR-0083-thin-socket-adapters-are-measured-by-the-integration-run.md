# ADR-0083 — The thin socket adapters are measured by the Integration run

- **Status:** Accepted. Superseded in part by ADR-0421 (where Integration tests live; which socket tests are unit tests).
- **Date:** 2026-09-27

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-357.

## Context

The 100% line and branch gate is measured on the fast run
(`dotnet test --filter "TestCategory!=Integration"`), and the product rule is that unit
tests need no network (`Documentation/Product/Product-Overview.md`, "Unit tests need no
network"). Three members of `Curl.Networking.UnitLibrary` exist only to hand bytes to a
real socket:

- `TcpDialer.DialAsync`, which connects a TCP `Socket` and wraps it in a `NetworkStream`.
- `UdpDatagramChannel.SendAsync`, which sends one datagram.
- `UdpDatagramChannel.ReceiveAsync`, which waits for one datagram to arrive.

Past their argument checks, none of them can run without a connected socket or a datagram
on the wire, so the fast run reached 14%, 50% and 60% of them (BL-354). Their loopback tests
in `TcpDialerTests` and `UdpDatagramChannelTests` are tagged `Integration` and cover every
line.

## Decision

- The three members carry `[ExcludeFromCodeCoverage]` with a `Justification` naming this
  ADR, and a `<remarks>` saying which Integration test measures them.
  `Measure-CodeQuality.ps1` lists every such exclusion under "Coverage exclusions in
  production code", so they stay visible in every audit and in the published report.
- Their argument checks, and everything else in both types (the constructors, the bind
  failure, cancellation and disposal), stay measured and are covered by fast tests that open
  local sockets without sending anything.
- The exclusion is for these three members only. Any other member that talks to a socket
  goes behind a seam that a fake can drive, as `UdpDatagramConnector` and
  `SslStreamTlsProvider` already do.

## Consequences

- `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` passes on the fast run, and
  the fast run still sends nothing.
- A regression in these three members is caught only by `dotnet test` with the Integration
  tests included, not by the fast run.
- The members must stay thin. Logic added to them would be unmeasured; it belongs in a
  measured type instead.

## Alternatives considered

- **Put a seam between these members and `Socket`.** This lost because the default
  implementation of the seam is itself the socket call, so the uncovered lines move into a
  lambda rather than going away, and the members gain indirection nobody else needs.
- **Run loopback round trips in the fast run.** This lost because it puts bytes on a socket
  in the unit tests, against the rule that unit tests need no network.
- **Teach `Measure-CodeQuality.ps1` a list of members to skip.** This lost because the
  attribute does the same job, sits beside the code it excuses, and is already reported by
  the script.
