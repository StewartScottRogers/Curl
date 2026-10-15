# ADR-0522: The HTTPS stand-in answers a TLS 1.3-only hello with Curl's own TLS 1.3 server where SslStream cannot

- Status: Accepted
- Date: 2026-10-15
- Task: BL-2335 (gap finding GF-0092)
- Decided by Claude under Stewart's delegation.

## Context

Upstream test4001 runs `--ech ecl:<config list> -k https://%HOSTIP:%HTTPSPORT/4001` against
stunnel, which holds no ECH keys, and expects exit 101 (ECH required) from an OpenSSL build. With
an `ecl:` list Curl's hand-built TLS client offers TLS 1.3 only, as curl's OpenSSL build does
(BL-1950). The conformance harness's stunnel stand-ins (`TlsServerStream` in
`Curl.Conformance.UnitLibrary`) are `SslStream` servers, and on macOS .NET's `SslStream` server
negotiates at most TLS 1.2. So on macOS the handshake failed on the protocol version (exit 35)
before the client could learn that the server ignored its ECH offer; Windows and Linux, whose
`SslStream` servers speak TLS 1.3, measured the case as `match`. The cause was the harness, not
Curl.

## Decision

1. `TlsServerStream.AcceptAsync(innerStream, options, platformServesTls13, cancellationToken)`
   is what the HTTPS and TLS-relay stand-ins call, with `TlsServerStream.PlatformServesTls13`
   (false on macOS only). Where the platform serves TLS 1.3 it is `AuthenticateAsync`, unchanged.
2. Elsewhere it reads the client's first record. A ClientHello whose `supported_versions` offers
   nothing below TLS 1.3, with no client certificate asked for, is answered by
   `Tls13ServerStream`, built from `Curl.Tls.UnitLibrary`'s codecs and key schedule:
   TLS_AES_128_GCM_SHA256, the client's X25519 share (no HelloRetryRequest), the first of the
   server's ALPN protocols the client offers, and CertificateVerify signed with the certificate's
   ECDSA key, as every certificate upstream's `genserv.pl` (and `UpstreamTestCertificateGenerator`)
   makes is P-256. It ignores Encrypted Client Hello, as stunnel without ECH keys does. Any other
   first record is handed back to `SslStream` through `ReplayedPrefixStream`, so every case that
   matched on macOS before is served exactly as before.
3. `Curl.Conformance.UnitLibrary` references `Curl.Tls.UnitLibrary`, which is not a protocol
   library, so its rule of referencing no protocol library still holds.

## Consequences

- On macOS test4001 meets a TLS 1.3 server that ignores ECH, and Curl exits 101 as curl does;
  `Tls13ServerStreamTests.AcceptAsync_EchOfferedToHandBuiltServer_ClientAbortsWithEchRequired`
  pins it on every platform. GF-0092 closes only when a later gap run measures it `match`.
- The hand-built server asks for no client certificate, resumes no session and sends no tickets;
  a TLS 1.3-only case needing one of those still goes to `SslStream` on macOS.
