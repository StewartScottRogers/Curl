# ADR-1233: DNS over QUIC refuses UDP port 53 with exit 7

- Status: Accepted
- Date: 2026-11-03
- Task: BL-4723
- Decided by Claude under Stewart's delegation

## Context

RFC 9250 section 4.1.1 says "DoQ connections MUST NOT use UDP port 53". `dns://host:53/name --dns-transport quic`
opened the QUIC connection and answered with exit 0, so conformance case `DnsQuery:RFC9250-4.1.1-2` failed. Upstream
curl has no DNS over QUIC, so there is no curl output to measure; the exit code and message are Curl's choice.

## Decision

`DnsQuicTransport.ExchangeAsync` refuses port 53 before asking the connector for a channel, so no datagram is sent,
and fails with exit 7 (`CURLE_COULDNT_CONNECT`) and the message
`DNS over QUIC must not use UDP port 53 (RFC 9250 section 4.1.1)`.

Exit 7 because no connection to the server was made, which is what a script reading curl's exit codes would take 7
to mean; exit 3 (`CURLE_URL_MALFORMAT`) would be wrong, as the same URL is valid under `--dns-transport udp`, `tcp`
or `tls`. The other transports keep accepting port 53, which RFC 9250 alone forbids.

## Consequences

The refusal is checked in the transport, so a URL naming no port (853 for DoQ) is unaffected. The conformance case
pins the exit code and stderr and no longer appears in `KnownFailures.txt`.
