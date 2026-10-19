# ADR-0703: CURLOPT_SSL_VERIFYHOST without CURLOPT_SSL_VERIFYPEER still checks the name

- Status: Accepted
- Date: 2026-10-19
- Decided by Claude under Stewart's delegation
- Task: BL-2692

## Context

libcurl keeps `CURLOPT_SSL_VERIFYPEER` and `CURLOPT_SSL_VERIFYHOST` apart: with the peer not
verified and the host check on, curl 8.22.0 still refuses a certificate that does not name the
host, with `CURLE_PEER_FAILED_VERIFICATION` (exit 60). Upstream test2118 (lib2118) pins it against
a server whose certificate is for `localhost.nn`. Curl mapped only `!VerifyPeer` onto
`TlsClientOptions.Insecure`, under which `ServerCertificateVerification.VerifyPeer` accepted any
certificate, so the case ended with exit 52.

The curl tool's `-k` turns both off, so only `Libcurl` and `Curl.Api` handles can ask for this mix.

## Decision

`TlsClientOptions` gains `ChecksHostNameWhenInsecure` (default `false`, so `-k` and every other
caller keep today's behaviour). `TlsClientOptionsMapping.Of` and `ProxyOf` set it when
`TlsPeerOptions.VerifyPeer` is off and `VerifyHost` on. Under `Insecure` with it set,
`ServerCertificateVerification.VerifyPeer` runs the build's name check as usual, keeps only
`RemoteCertificateNameMismatch` from the errors, and refuses with the build's exit 60 text for that
bit alone; the chain's errors are ignored as `Insecure` always ignored them.

Left as they are: the reverse mix (peer verified, host check off) and the TLS session cache key's
`:NO-VRFY-HOST`, which still follows `Insecure` alone; and the DoH handshake, whose
`DohVerifyHost` is not mapped.

## Consequences

Case 2118 passes and is held on the ratchet. Its only output is the exit code, which does not
depend on the platform: `SslStream` reports the name mismatch apart from the chain's errors on
Windows, Linux and macOS, and the hand-built verifier computes both.
