# ADR-1230: DNS over TLS queries carry EDNS(0) Padding to a 128-octet block

- Status: Accepted
- Date: 2026-11-03
- Task: BL-4689
- Decided by Claude under Stewart's delegation

## Context

RFC 8310 section 9 recommends that a DNS over TLS client pads its queries with the EDNS(0) Padding option (RFC 7830),
so that their length gives away less about the name asked for. RFC 8467 section 4.1 recommends the block-length
policy: pad each query to a multiple of 128 octets. `DnsTlsTransport` sent the query unpadded, with no OPT record, and
the conformance case `DnsQuery:RFC8310-9-1` failed. Upstream curl has no `dns:` scheme, so the RFCs decide.

## Decision

- `Wire/DnsEdnsPadding.Pad` appends an OPT pseudo-record (RFC 6891 section 6.1.2) to an encoded query and adds one to
  ARCOUNT. The record has the root name, TYPE 41, CLASS 1232 (the UDP payload size DNS Flag Day 2020 settled on),
  TTL 0 (extended RCODE 0, version 0, DO clear), and one option: Padding (code 12), whose zero octets bring the whole
  message to the next multiple of 128 octets. A query already on a block boundary still carries an empty Padding
  option, so every DoT query has the same shape.
- `DnsTlsTransport` pads the query it is handed just before it frames and sends it. The handler and the other
  transports are unchanged: the message ID and the question are the handler's, and only the octets on the TLS wire
  grow.
- DNS over HTTPS and DNS over QUIC are encrypted too, and RFC 8467 section 4.1 covers them. They are left unpadded
  here and their padding is follow-up work, because this task touches only the DoT transport and the wire codec.

## Consequences

Every `--dns-transport tls` query is 128 octets long, or 256 for a name long enough to need it. A server answers with
its own OPT record in the additional section, which the text and JSON renderers show as an `OPT` record.
