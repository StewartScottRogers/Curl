# Curl.Tls.UnitLibrary

A hand-built TLS client for what `SslStream` cannot do, and for QUIC. ADR-0140
(`Documentation/Planning/Decisions/ADR-0140-the-hand-built-tls-client-runs-quic-always-and-tcp-only-where-sslstream-cannot.md`)
decides what it does and when Curl uses it: always inside QUIC, where RFC 9001 hands
handshake bytes to CRYPTO frames and takes secrets per encryption level, and over TCP
only for what `SslStream` cannot offer (`--curves`, `--sigalgs`, `--tls-earlydata`,
`--ssl-sessions`, `--no-sessionid`, `--ech`, TLS-SRP, `--cert-status`, TLS 1.0 and 1.1
where the operating system disables them, `--ssl-allow-beast`). Everything else stays on
`SslStreamTlsProvider` in `Curl.Networking.UnitLibrary`.

Namespace `Curl.Tls`. The project is empty until its first type lands (BL-699 onwards).

## Rules

- **Base class library plus `Curl.Cryptography.UnitLibrary` only** (ADR-0120). It may
  also reference `Curl.Protocol.Abstractions.UnitLibrary`; nothing else. The
  `Curl.Cryptography.UnitLibrary` reference is added by the first task that needs it.
  `Curl.Quic.UnitLibrary` and `Curl.Networking.UnitLibrary` reference this library, never
  the other way round.
- **Never a `Socket` or `SslStream`**, and no `HttpClient`. Bytes in, bytes out: record
  layer bytes and handshake messages go in and come out, and the caller owns the
  transport (a TCP connection, or QUIC's CRYPTO frames).
- **Randomness and time are injected.** Client randoms, key shares and session IDs come
  from an injected source, and certificate validity and ticket lifetimes are judged
  against an injected `TimeProvider`, so every handshake is reproducible in a test.
- **RFC 8448 traces are the reference tests.** The example handshakes in RFC 8448 (and
  RFC 9001 appendix A for QUIC) are replayed byte for byte in `Curl.Tls.UnitTests`, with
  the section cited beside each trace. Tests are platform-neutral and never open a
  socket.
- Same quality gates as every library: 100% line and branch coverage, cyclomatic
  complexity of at most 10, CRAP of at most 30.
