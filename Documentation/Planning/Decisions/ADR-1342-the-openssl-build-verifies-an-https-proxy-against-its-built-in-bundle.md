# ADR-1342: The OpenSSL build verifies an HTTPS proxy against its built-in bundle

- Status: Accepted
- Date: 2026-11-23
- Decided by Claude under Stewart's delegation (BL-5828, fitness finding STD-178)

## Context

On Linux, curl 8.18.0 (OpenSSL) verifies an HTTPS proxy against its built-in bundle:
`CURL_CA_BUNDLE`, `SSL_CERT_FILE` and `--cacert` name the target's CA, so a proxy chaining to
that private root fails with exit 60 `(20)`. `SSL_CERT_DIR` does reach the proxy. Curl read
`CURL_CA_BUNDLE` for every handshake with no CA option, the proxy's too, and .NET's OpenSSL store
read `SSL_CERT_FILE` itself.

## Decision

`ServerCertificateVerification.IsProxy` (set by `ForProxy` / `ForHandshake(..., isProxy)`, which
both TLS providers call when the handshake is with an HTTPS proxy) makes the default bundle the
first existing `DefaultCaBundleCandidates` file, ignoring `CURL_CA_BUNDLE` and `SSL_CERT_FILE`.
With a non-empty `SSL_CERT_DIR` it keeps the system store, which reads that directory. The Schannel
build is unchanged.

## Consequences

The four STD-178 commands exit 60; `SSL_CERT_DIR` still exits 0. With `SSL_CERT_DIR` and
`SSL_CERT_FILE` both set the proxy also trusts the file: an edge not measured.
