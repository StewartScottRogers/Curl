# ADR-1228: DNS over HTTPS queries carry DNS ID 0

- Status: Accepted
- Date: 2026-11-03
- Task: BL-4688
- Decided by Claude under Stewart's delegation

## Context

ADR-1148 added DNS over HTTPS (RFC 8484) as `--dns-transport https`. `DnsQueryProtocolHandler` drew a random message
ID for every transport, but RFC 8484 section 4.1 says a DoH client using `application/dns-message` SHOULD use DNS ID 0
in every request, so that identical queries are identical HTTP requests and HTTP caches can serve them. The conformance
case `DnsQuery:RFC8484-4.1-2` failed. Upstream curl has no `dns:` scheme, so the RFC decides.

## Decision

- `IDnsQueryTransport` gains `QueriesWithMessageIdZero`, false by default. Only `DnsHttpsTransport` sets it to true.
- The handler encodes the query with ID 0 when the transport asks for it, and otherwise with a random ID. UDP, TCP
  and DNS over TLS keep the random ID that RFC 5452 section 4 asks for against spoofing. DoH does not need it, because
  TLS already binds the answer to the request.
- The handler still checks that the answer's ID matches the query's ID, so a DoH answer must carry ID 0. The written
  header shows that ID (`id: 0`), which is what was on the wire.
- DNS over QUIC is unchanged. Its transport already rewrites the ID to 0 on the wire (RFC 9250 section 4.2.1) and gives
  the answer back with the query's random ID.

## Consequences

A DoH query for the same name is the same byte string every time. A server that answers a DoH query with an ID other
than 0 gets exit 8 (`CURLE_WEIRD_SERVER_REPLY`), as an ID mismatch over any other transport does.
