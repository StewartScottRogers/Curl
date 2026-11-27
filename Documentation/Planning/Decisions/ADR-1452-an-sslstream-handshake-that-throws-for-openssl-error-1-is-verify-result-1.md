# ADR-1452: An SslStream handshake that throws for OpenSSL error 1 is verify result 1

- Status: Accepted
- Date: 2026-11-26
- Decided by Claude under Stewart's delegation (BL-6063)

## Context

ADR-1446 covers the chain builds Curl makes itself. On Linux `SslStream` also builds the chain
inside its handshake, and for `X509_V_ERR_UNSPECIFIED` (1) that build throws
`CryptographicException`, "... unhandled error code '1'.", before the validation callback runs.
The handshake then looked like a connect error, exit 35. OpenSSL curl reports the chain as exit 60,
`unspecified certificate verification error (1)`, and under `-k` goes on with the transfer.

## Decision

- In the OpenSSL build, a handshake that throws that exception, bare or as an inner exception, is
  exit 60 with `SSL certificate OpenSSL verify result: unspecified certificate verification error
  (1)`, and verify result 1 for `%{ssl_verify_result}` or `%{proxy_ssl_verify_result}`.
  `CertificateChainBuild` recognises it, so .NET's message is matched in one place.
- Under `-k`, `SslStreamTlsProvider` reports verify result 1 and keeps the connect error, exit 35.
  The handshake is already over when the exception arrives, the validation callback never ran, and
  the provider was handed one connection, so it has nothing to go on with.
- The certificate is not available at that point, so neither `--pinnedpubkey` nor the host name is
  judged.
- The Schannel build is unchanged.

Matching curl under `-k` needs a second connection whose handshake runs on the hand-built client,
which builds the chain through `CertificateChainBuild` and goes on under `-k`. The dialling belongs
to the connector, not the provider, so it is its own task (BL-6066).

Rejected: running every `-k` handshake of the OpenSSL build on the hand-built client. It would
change the bytes of every `-k` transfer (ADR-0140) for a certificate fault that is very rare.

## Consequences

- Until BL-6066 is done, `-k` against such a server ends with exit 35 where curl succeeds.
- A chain that both mismatches the host and ends with error 1 is reported as error 1, as ADR-1446
  has it.
