# ADR-1024: The DNS-over-TLS resolver pipelines AAAA and A over one TLS connection

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4207)

## Context

BL-4148 (an encrypted DNS extension, ADR-0999) was split for size; BL-4207 builds its first part,
`Curl.Extension.EncryptedDns.UnitLibrary`, with a DNS message codec and an `IDnsResolver` over DNS over TLS
(RFC 7858). Upstream curl has no DNS-over-TLS option, so there is nothing to measure; the code is tested against
RFC 1035 and RFC 7858. `Curl.Networking.UnitLibrary`'s DoH codec (ADR-0152) is internal, and an extension may not
reference that library.

## Decision

- The extension writes its own codec in `Wire/`: A and AAAA queries per RFC 1035 section 4.1 (recursion desired,
  one question, QCLASS IN), answers decoded with compression pointers, and failures for a malformed message, a
  message that is not the answer (other ID or QR clear), the TC bit and a non-zero RCODE.
- `DnsOverTlsResolver` takes an `IConnector` and an `ITlsProvider`, connects with `UseTls: false` to the server's
  port (853 by default, section 3.1), runs the handshake before writing any byte (section 3.2), and verifies the
  server's certificate against an optional TLS server name, else the server's host.
- One connection per lookup carries both queries pipelined, AAAA with ID 2 and A with ID 1, each with the two-octet
  length prefix (section 3.3); the two answers are matched by ID, in whatever order they arrive. Fixed IDs are safe
  here because the channel is authenticated TLS, where a random ID adds nothing against spoofing, and they keep the
  bytes on the wire deterministic for tests.
- The addresses come back AAAA first, then A, as `DohDnsResolver` returns them. An address literal is answered
  without a query. Every failure (a name RFC 1035 cannot carry, a failed connect or handshake, a server closing
  inside a message, failed answers) gives no addresses, which `TcpConnector` reports as exit 6, as for DoH.
- Connection reuse across lookups and the options, hook and catalogue lines are left to BL-4148.
