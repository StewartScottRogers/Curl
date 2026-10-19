# ADR-0704: CURLOPT_CAINFO_BLOB is trusted in place of the CA file

- Status: Accepted
- Date: 2026-10-19
- Decided by Claude under Stewart's delegation
- Task: BL-2725

## Context

libcurl's `CURLOPT_CAINFO_BLOB` (and `CURLOPT_PROXY_CAINFO_BLOB`) hands the CA certificates over in
memory. `Libcurl` stored the blob on `TlsPeerOptions.CertificateAuthorityBlob`, but
`TlsClientOptionsMapping` mapped only the file, so the handshake verified against the system store and
upstream test678 (lib678, the test CA as a blob) ended with exit 60 before any request.

## Decision

`TlsClientOptions` gains `CaCertificateBlob`. `TlsClientOptionsMapping.Of` and `DohOf` carry the
origin's blob (libcurl's `lib/doh.c` copies `CURLOPT_CAINFO_BLOB` onto each DoH transfer) and `ProxyOf`
the proxy's. `ServerCertificateVerification` treats a blob as a CA file (`TlsClientOptions.HasCaCertificates`):
the chain must lead to its certificates in place of the system store, read as PEM text by the same
rules as the file, and the blob wins when both are set, as libcurl's OpenSSL and Schannel backends
prefer it. Every place that asked whether a `--cacert` file was given (the Schannel pin order, name
check and exit 60 text, QUIC's Windows-store trust) now asks `HasCaCertificates`. `DescribeTrust` sets
`TlsTrustEvent.HasCaCertificateBlob`, so the OpenSSL build's `-v` prints `CA Blob from configuration`.

Left as they are: a blob that does not parse reports the file's exit 77 text with no file name, not
measured against curl; the TLS session cache key does not include the blob.
