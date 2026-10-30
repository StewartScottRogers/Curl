# ADR-1112: The MASQUE extension's wire format follows RFC 9298 and RFC 9484 over an HTTP/1.1 upgrade

- Status: Accepted
- Date: 2026-10-29
- Task: BL-4319
- Decided by Claude under Stewart's delegation

## Context

The MASQUE extension (ADR-0999) proxies UDP (CONNECT-UDP, RFC 9298) and IP (CONNECT-IP, RFC 9484)
through an HTTP proxy. BL-4144 was split into the wire format (BL-4319), the client flow (BL-4317)
and the option (BL-4144). Upstream curl has no MASQUE extension, so the library is tested against
the RFCs, not against curl. An extension may not reference a protocol library, so HTTP/2 and HTTP/3
extended CONNECT are out of its reach; the HTTP/1.1 upgrade (RFC 9298 section 3.2, RFC 9484
section 4.5) needs only a byte stream.

## Decision

- `Curl.Extension.Masque.UnitLibrary` references `Curl.Protocol.Abstractions`,
  `Curl.Extension.Abstractions` and the shared `Curl.HttpDatagram` (ADR-1004). Its types are
  internal until the client flow needs more.
- URI templates: the RFC 6570 expressions `{a,b}`, `{?a,b}` and `{&a,b}`, values percent-encoded
  as simple expansion does (everything but unreserved characters, so `::1` is `%3A%3A1` and the
  wildcard `*` is `%2A`). A CONNECT-UDP template must name both `target_host` and `target_port`
  (RFC 9298 section 2). A CONNECT-IP template must name `target`, `ipproto` or both: RFC 9484
  allows a template with neither, but Curl refuses it, so a template meant for something else is
  not taken for a tunnel to everything. A variable other than the pair, another operator, an
  unclosed brace, or a result that is not an absolute `http` or `https` URI is refused.
- The upgrade request is `GET` in absolute form, as the RFCs' examples and curl's own `-x`
  CONNECT-UDP request (ADR-0250) send it, with `Host`, `Connection: Upgrade`, `Upgrade:
  connect-udp` or `connect-ip`, and `Capsule-Protocol: ?1`. The reply opens the tunnel only on
  `101` carrying the same `Upgrade` token and `Connection: Upgrade` (tokens compared ignoring
  case); any other status is refused with that status.
- `ADDRESS_ASSIGN` (0x01), `ADDRESS_REQUEST` (0x02) and `ROUTE_ADVERTISEMENT` (0x03) are decoded
  strictly: an IP version other than 4 or 6, a field cut short, a prefix length longer than the
  address, an `ADDRESS_REQUEST` with no entry or a request ID of 0, and route ranges that end
  before they start, are out of order (IP version, then IP protocol, then start) or overlap are
  all malformed. Host bits beyond the prefix length are accepted as sent.
- IP packets carry one UDP datagram: IPv4 with a 20-byte header, Don't Fragment, TTL 64 and its
  header checksum; IPv6 with a hop limit of 64 and no extension headers; UDP with its checksum
  over the pseudo-header, sent as `0xFFFF` when it computes to 0 (RFC 768). Reading back ignores
  any packet that is not one whole UDP datagram: other protocols, IPv4 fragments, IPv6 extension
  headers and truncated packets. Incoming checksums are not checked; the proxy delivered the
  packet over a reliable, authenticated stream.
- HTTP Datagram payloads use Context ID 0 only; any other is dropped, as both RFCs allow.

## Consequences

- The client flow (BL-4317) gets each piece as bytes in, bytes out, with no stream or socket.
- Supporting HTTP/2 or HTTP/3 extended CONNECT later needs a seam in the extension abstractions,
  not a change to this wire format.
