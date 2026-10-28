# ADR-0956: The OpenSSL build always gives SslStream a certificate selection callback

- Status: Accepted
- Date: 2026-10-28
- Task: BL-3698 (CGR-169)
- Decided by Claude under Stewart's delegation.

## Context

curl's OpenSSL build offers `post_handshake_auth` in every TLS 1.3 ClientHello and, without
`--cert`, answers a post-handshake CertificateRequest with an empty Certificate and a
Finished. Measured with curl 8.18.0 (OpenSSL 3.5.5) in WSL against
`openssl s_server -tls1_3` sending the request (its `c` command) after the HTTP request:
exit 0, body `reneg`, the server logs `Certificate [length 0028]` and `Finished` from the
client.

Curl's OpenSSL build runs a plain `https://` transfer through `SslStream`
(`SslStreamTlsProvider`), which gave `SslStream` a `LocalCertificateSelectionCallback` only
with `--cert` (BL-254). On Linux `SslStream` offers `post_handshake_auth` only when it can
choose a certificate, so without `--cert` the hello carried no such extension and the
server's `SSL_verify_client_post_handshake` failed with `extension not received`; a server
that needs the answer (per-location `SSLVerifyClient optional` over TLS 1.3) aborts.

## Decision

The OpenSSL build always passes a selection callback: it returns the `--cert` certificate
when there is one and none otherwise, so `SslStream` offers `post_handshake_auth` and
answers a request with an empty Certificate, as curl does. The Schannel build keeps BL-254's
rule (a callback only with `--cert`): Schannel offers no post-handshake authentication, and
curl's Schannel build none either.

## Consequences

Measured again with a linux-x64 build of Curl: the server logs the same empty Certificate
and Finished as for curl, exit 0, body `reneg`. The hand-built client already offered and
answered it (BL-880). Curl's `SslStream` path still closes without `close_notify` where curl
sends one; that is filed separately.
