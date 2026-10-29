# ADR-0991: The Schannel build answers CURLE_NOT_BUILT_IN for TLS13_CIPHERS and PROXY_CAPATH

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4011)

## Context

CGR-265: Libcurl took `CURLOPT_TLS13_CIPHERS`, `CURLOPT_PROXY_TLS13_CIPHERS` and
`CURLOPT_PROXY_CAPATH` with `CURLE_OK` on every platform. Measured 2026-10-28 with curl 8.22.0
(Schannel) on Windows through the tool: `--tls13-ciphers` and `--proxy-tls13-ciphers` warn
"ignoring --tls13-ciphers, not supported by libcurl with Schannel", and `--capath` warns only
"ignoring setting the CA path for the proxy", so libcurl answers `CURLE_NOT_BUILT_IN` for those
three and `CURLE_OK` for `CURLOPT_CAPATH`. The OpenSSL build (Linux, macOS) takes all four.
BL-4029 made the curl tool warn and carry on when libcurl answers `CURLE_NOT_BUILT_IN` for them.

## Decision

`EasyTlsOptionSetters` gates the three options on `Tls13CiphersIsBuiltIn` and
`ProxyCaPathIsBuiltIn`, both `!OperatingSystem.IsWindows()`, the same shape as
`EasyAuthenticationOptionSetters.SrpIsBuiltIn` (BL-3794). Off, the option answers
`CURLE_NOT_BUILT_IN` for every value, `NULL` included, and changes nothing, as `setopt.c`'s
`Curl_ssl_supports` check does.

## Consequences

`Cgr265RemainderTests` in `Libcurl.UnitTests` (`[DoNotParallelize]`, since it flips the
process-wide flags) pins both flag values on every platform. The other options CGR-265 lists
(`DNS_*`, `ECH`, `CRLFILE`, `ISSUERCERT`, `SSL_EC_CURVES`, `SSL_SIGNATURE_ALGORITHMS`,
`SSL_VERIFYSTATUS`, `HTTP_VERSION` 3 and above, `STREAM_WEIGHT`, `TCP_FASTOPEN`, `KRBLEVEL`,
`PROXYTYPE` 8, Linux `SSLENGINE`, `HTTPSIG_*`) need a libcurl-level measurement the tool cannot
give, and are left to a follow-up task.
