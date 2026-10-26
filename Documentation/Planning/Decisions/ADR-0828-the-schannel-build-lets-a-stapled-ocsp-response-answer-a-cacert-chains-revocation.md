# ADR-0828: The Schannel build lets a stapled OCSP response answer a --cacert chain's revocation

- Status: Accepted
- Date: 2026-10-26
- Decision: Decided by Claude under Stewart's delegation (BL-3543)
- Relates to: [ADR-0140](ADR-0140-the-hand-built-tls-client-runs-quic-always-and-tcp-only-where-sslstream-cannot.md), [ADR-0173](ADR-0173-the-hand-built-tls-client-checks-a-stapled-ocsp-response-in-openssls-order-and-fails-the-handshake-with-a-typed-outcome.md), [ADR-0321](ADR-0321-the-schannel-build-checks-revocation-for-a-cacert-chain.md)

## Context

CGR-014: on Windows curl 8.21.0 (Schannel) uses the OCSP response a server staples. Measured with
`Record-StapledOcspExchange.ps1` (BL-3826) against `openssl s_server -status_file`, the leaves
carrying an OCSP URL nothing answers: a good staple is exit 0 with or without
`--ssl-revoke-best-effort` and `--cert-status`; a revoked staple is exit 60, `schannel: trust for
this certificate or one of the certificates in the certificate chain has been revoked`, under
every one of those flags. Curl ignored the staple: a good one failed with `the revocation status is
unknown`, and `--ssl-revoke-best-effort` let a revoked one through.

`SslStream` never hands the staple over: a probe found no `CERT_OCSP_RESPONSE_PROP_ID` on the
certificate its validation callback gets, and the chain it builds reports the status unknown. The
hand-built client's handshake (ADR-0140) receives it: the Schannel ClientHello profile offers
`status_request`, and `ServerCertificateChain.OcspResponse` carries the staple to the verifier.

## Decision

1. `TlsClientRouting` gains a last row: in the Schannel build, a connection with `--cacert` (or a
   CA blob) that checks revocation (no `--ssl-no-revoke`, no `-k`) runs on the hand-built client
   (`ReasonInBuild`; `Reason` and `Choose` take the build from `OperatingSystem.IsWindows()`).
2. `ServerCertificateVerification.Judge` takes the staple. In the Schannel build, unless `-k` or
   `--ssl-no-revoke`, `OcspStapleVerifier` judges it, with the server's certificates and the chain
   built (which holds the `--cacert` root a server leaves out) as issuer candidates:
   - good: a chain whose only faults are an unknown or offline revocation status is accepted, and
     the check is not reported incomplete;
   - revoked: exit 60 with `TlsFailureMessages.SchannelChainRevoked` with `--cacert`, else the
     handshake's `CRYPT_E_REVOKED`, exit 35, whatever `--ssl-revoke-best-effort` says;
   - anything else (unknown status, a staple that does not verify) leaves the online check to decide.
3. The OpenSSL build reads a staple only for `--cert-status`, as before.

## Consequences

Every Windows transfer with `--cacert` that checks revocation now runs the hand-built TLS client,
whose hello is Schannel's measured profile. A good staple answers only for the server's own
certificate's status; an intermediate's unknown status is masked with it, since .NET reports the
chain's status as one set (a known difference from CryptoAPI's per-element check). The system-store
path (no `--cacert`) keeps `SslStream`, so a staple there is still unused.
