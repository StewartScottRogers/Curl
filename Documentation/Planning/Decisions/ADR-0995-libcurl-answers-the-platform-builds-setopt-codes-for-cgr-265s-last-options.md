# ADR-0995: Libcurl answers the platform build's setopt codes for CGR-265's last options

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4066)

## Context

CGR-265's last options (after ADR-0991) were taken with `CURLE_OK` on every platform. The curl
tool cannot show libcurl's answer for them, so each was read from curl 8.22.0's `lib/setopt.c`
and `lib/vtls` (the copy under `Curl.Conformance.UnitTests/CurlTestCases/Original/lib`), with each
`#ifdef` and `Curl_ssl_supports` check resolved for the Schannel build (`curl -V` here: 8.22.0,
Schannel, no c-ares, no HTTP2/HTTP3, no ECH) and the OpenSSL build of Linux and macOS:

| Option | Schannel (Windows) | OpenSSL (Linux, macOS) | Why |
|---|---|---|---|
| `CRLFILE`, `PROXY_CRLFILE` | `NOT_BUILT_IN` | `OK` | `SSLSUPP_CRLFILE` |
| `ISSUERCERT`, `PROXY_ISSUERCERT`, both `_BLOB`s | `NOT_BUILT_IN` | `OK` | `SSLSUPP_ISSUERCERT(_BLOB)` |
| `SSL_EC_CURVES` | `NOT_BUILT_IN` | `OK` | `SSLSUPP_SSL_EC_CURVES` |
| `SSL_SIGNATURE_ALGORITHMS` | `NOT_BUILT_IN` | `OK` | `SSLSUPP_SIGNATURE_ALGORITHMS` |
| `SSLENGINE` (a name) | `NOT_BUILT_IN` | `SSL_ENGINE_NOTFOUND` | no `set_engine`; OpenSSL finds no engine or provider Curl can load |
| `SSLENGINE_DEFAULT` | `NOT_BUILT_IN` | `OK` | no `set_engine_default`; OpenSSL's with no engine set |
| `TCP_FASTOPEN` | `NOT_BUILT_IN` | `OK` | no `CONNECT_DATA_IDEMPOTENT`, `MSG_FASTOPEN` or `TCP_FASTOPEN_CONNECT` on Windows |
| `KRBLEVEL` | `NOT_BUILT_IN` | `NOT_BUILT_IN` | removed in 8.17.0 |
| `PROXYTYPE` 8 (`CURLPROXY_HTTPS3`) | `NOT_BUILT_IN` | `NOT_BUILT_IN` | no `USE_PROXY_HTTP3` |
| `SSL_VERIFYSTATUS`, `DOH_SSL_VERIFYSTATUS` | `NOT_BUILT_IN` | `OK` | no `cert_status_request` in Schannel |
| `ECH` | `NOT_BUILT_IN` | `NOT_BUILT_IN` | no `USE_ECH` in either build |
| `DNS_SERVERS`, `_INTERFACE`, `_LOCAL_IP4`, `_LOCAL_IP6` | `UNKNOWN_OPTION` | `UNKNOWN_OPTION` | no `USE_RESOLV_ARES` |
| `HTTP_VERSION` 3, 4, 5, 30, 31 | `OK` with HTTP2/HTTP3 | `OK` | Libcurl advertises HTTP2 and HTTP3 |
| `STREAM_WEIGHT` | `OK` | `OK` | `USE_HTTP2` |
| `HTTPSIG_*` | `OK` | `OK` | not `CURL_DISABLE_HTTPSIG` |

## Decision

Each option whose capability Curl lacks, or takes only from the platform's TLS backend, answers
the platform build's code, behind one injectable flag per capability as `Tls13CiphersIsBuiltIn`
does: `RevocationListFileIsBuiltIn`, `IssuerCertificateIsBuiltIn`, `EllipticCurvesIsBuiltIn`,
`SignatureAlgorithmsIsBuiltIn` and `OpenSslEngineIsBuiltIn` (`EasyTlsOptionSetters`) and
`TcpFastOpenIsBuiltIn` (`EasyNetworkOptionSetters`), each `!OperatingSystem.IsWindows()`.
`KRBLEVEL` and `PROXYTYPE` 8 answer `NOT_BUILT_IN` everywhere.

Where Curl builds the capability itself, it keeps it: OCSP stapling (`CertificateStatusIsBuiltIn`),
ECH (`EncryptedClientHelloIsBuiltIn`) and its own resolver's DNS servers (`AresIsBuiltIn`) default
on everywhere, as do HTTP/2, HTTP/3, stream weights and HTTP signatures. Turning them off broke the
working `--cert-status` and `--ech` paths of `Curl.Console` (which sets options through Libcurl),
upstream cases 4000 and 4001 on the conformance ratchet, and lib1592's cancellable-resolver path:
a drop-in replacement that does more than the platform build is still a drop-in replacement,
while one that refuses a feature it has removes it. The flags still let tests pin the build
without them.

## Consequences

`Cgr265LastPartTests` in `Libcurl.UnitTests` (`[DoNotParallelize]`) pins every flag both ways;
the tests of these options moved there from the option-group test classes. A future capability
Curl builds by hand (CRL files under Schannel, say) flips its flag's default.
