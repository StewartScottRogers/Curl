# ADR-1052: An extension verifies a server chain through ITlsProvider

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4249)

## Context

A protocol handler that runs its own handshake, such as the CoAP extension's DTLS 1.2 over UDP
(BL-4116, ADR-1042), receives the server's certificates but had no way to judge them as curl's own
TLS does. `ProtocolHandlerServices` gives it only `ITlsProvider`, which upgrades a stream;
`HandBuiltCertificateVerifier` and `ServerCertificateVerification` are internal to
`Curl.Networking.UnitLibrary`, and the trust options (`-k`, `--cacert`, `--capath`, `--crlfile`,
`--pinnedpubkey`) are not on `ITransferContext`. `Curl.Protocol.Abstractions` references nothing,
so `Curl.Tls`'s `IServerCertificateVerifier` cannot be the contract.

## Decision

- `ITlsProvider` gains `VerifyServerCertificateChain(IReadOnlyList<ReadOnlyMemory<byte>> certificates,
  string targetHost)`, returning a new `ServerCertificateChainVerdict` in `Curl.Protocol.Abstractions`:
  `Accepted`, or `Rejected(exitCode, message)` with the curl exit code and message the handshake
  would fail with. The provider is the seam because it already holds the transfer's trust settings
  and is what every handler, extensions included, already receives; a method beats a verifier
  factory, since a stateless one-shot judgement is all a handler needs.
- It is a default interface method that rejects with exit 60, so a provider that cannot verify apart
  from a handshake (test fakes, decorators not yet forwarding it) never accepts an unchecked chain.
- `SslStreamTlsProvider` and `HandBuiltTlsProvider` override it with
  `ServerCertificateVerification.VerifyChain`, which reads the trust anchors as a handshake does and
  judges the chain with `HandBuiltCertificateVerifier`, the code the hand-built client hands its chain
  to. So `-k` accepts any chain, a chain the `--cacert` CA did not sign is exit 60, a pin mismatch exit
  90 and an unreadable `--cacert` exit 77, with the handshake's messages, in each build. No stapled
  OCSP response is passed, so `--cert-status` is not checked here; a DTLS handler wanting it is
  future work.
- Decorators outside `Curl.Networking` (`CertificateCheckingTlsProvider` in
  `Curl.Extension.Abstractions`) forward the method under their own task.

## Consequences

An extension's own handshake gets curl's verdicts without opening a TLS stream and without any new
reference. The default method keeps every existing `ITlsProvider` compiling.
