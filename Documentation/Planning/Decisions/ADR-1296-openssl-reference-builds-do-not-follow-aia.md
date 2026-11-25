# ADR-1296: The OpenSSL-reference builds do not follow AIA certificate links (BL-5396)

- Status: Accepted
- Date: 2026-11-17
- Decided by Claude under Stewart's delegation.

## Context

A server that omits its intermediate certificate is refused by OpenSSL curl on Linux and
macOS with exit 60 and `unable to get local issuer certificate (20)`, because OpenSSL does
not fetch the issuer from the Authority Information Access extension. Schannel does fetch
it, so the Windows build verifies the same server. Curl on those platforms returned 200
because .NET's chain builder downloads AIA issuers by default.

## Decision

When the build matches OpenSSL (`matchesSchannelBuild` false) every chain policy Curl builds
(the system store and the `--cacert` / `--capath` anchors) sets
`X509ChainPolicy.DisableCertificateDownloads = true`. The Schannel-flavoured build is
unchanged. Intermediates the server sends still verify. This tightens nothing in trust: the
chain must still reach a trusted root.
