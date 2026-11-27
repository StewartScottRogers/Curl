# ADR-1445: A chain build that throws for OpenSSL error 1 is verify result 1

- Status: Accepted
- Date: 2026-11-26
- Decided by Claude under Stewart's delegation (BL-6061)

## Context

On Linux .NET's `X509Chain.Build` runs OpenSSL's verification and maps each of its codes to a
`X509ChainStatusFlags` value. It has none for `X509_V_ERR_UNSPECIFIED` (1) and throws
`CryptographicException`, "OpenSSL mapping function MapOpenSsl30Code reported unhandled error code
'1'.", leaving the chain with no elements. OpenSSL answers 1 for a SmtpUTF8Mailbox under an
rfc822Name constraint that does not decode (ADR-1442). CI run 45917093887 showed the throw on
Ubuntu. OpenSSL curl reports that chain as exit 60, `unspecified certificate verification error (1)`.

## Decision

`CertificateChainBuild` runs every chain build Curl makes itself (the hand-built client's, the one
for a client certificate request and the rebuild against `--capath` anchors). It catches only the
exception whose message ends `unhandled error code '1'.`, answers that the chain did not verify and
remembers it. `OpenSslVerifyResult` and `TlsFailureMessages` report such an empty chain as verify
result 1. Every other `CryptographicException` propagates as before.

## Consequences

- With no chain elements there is no certificate to name a host mismatch from, so a chain that both
  mismatches the host and ends with error 1 is reported as error 1, where curl names the mismatch.
- The build `SslStream` makes inside its handshake does not go through `CertificateChainBuild`; its
  failure keeps the handshake's exit code.
- .NET's message is matched because the exception carries the OpenSSL code nowhere else. A .NET that
  maps code 1 to a status stops throwing, and the status path answers.
