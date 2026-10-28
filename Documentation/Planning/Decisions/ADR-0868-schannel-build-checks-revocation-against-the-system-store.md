# ADR-0868 — The Schannel build checks revocation against the system store

- Status: Accepted
- Date: 2026-10-22
- Task: BL-3215
- Decided by Claude under Stewart's delegation.

## Context

curl 8.22.0's Schannel build refuses `https://revoked.badssl.com/` with exit 35 and
`schannel: next InitializeSecurityContext failed: CRYPT_E_REVOKED (0x80092010) - The certificate is revoked.`
(measured by the tester, Stewart's 2026-10-22 findings list). Curl printed the page: without
`--cacert` `ServerCertificateVerification.ReadTrustAnchors` returned no chain policy, so
`SslStream` and the hand-built verifier built the chain with `X509RevocationMode.NoCheck`.
ADR-0321 already checks revocation for a `--cacert` chain. Schannel's default for a server
certificate checks the whole chain but the root online (CRL distribution points and OCSP),
which the OpenSSL builds on Linux and macOS never do by default.

## Decision

- Without `--cacert`, the Schannel build's chain policy trusts the system store, asks for the
  server authentication usage, and checks revocation `Online` with `ExcludeRoot`, unless
  `--ssl-no-revoke`. The OpenSSL build keeps no policy (`SslStream`'s `NoCheck`). On Windows
  `X509Chain` runs `CertGetCertificateChain`, the engine Schannel itself uses, so the fetching
  (CRL and OCSP, caching, timeouts) is Schannel's.
- A chain checked against the system store whose only fault is a revoked certificate (an
  unknown or offline status for another certificate aside) and whose name matches is exit 35
  with the measured `CRYPT_E_REVOKED` text, `--ssl-revoke-best-effort` or not: best effort
  tolerates only an unknown or offline status (BL-490).
- An unreachable responder without `--ssl-revoke-best-effort` stays the existing exit 60
  refusal; its exact Schannel text (`CRYPT_E_NO_REVOCATION_CHECK` or
  `CRYPT_E_REVOCATION_OFFLINE`, exit 35) was not measured in this task and is left for one
  that measures it.

## Consequences

- Tests cannot produce a revoked chain without a revocation server: Windows' chain engine
  does not fetch a `file://` distribution point (measured: unknown and offline status). So
  `ServerCertificateVerification.ReadChainStatus` is an internal `init` seam the tests replace
  with the status a revoked leaf or intermediate leaves; an unreachable responder is a real
  chain whose distribution points name missing files.
- A transfer against the system store on Windows now waits for revocation fetches, as curl's
  Schannel build does.
