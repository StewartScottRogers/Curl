---
id: BL-2296
title: Define EasyTlsOptions in Curl.Api.UnitLibrary with libcurl's defaults
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-2292]
touches: [Curl.Api.UnitLibrary/Options/Tls, Curl.Api.UnitTests/Options/Tls]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2296 — Define EasyTlsOptions in Curl.Api.UnitLibrary with libcurl's defaults

## Goal

`Curl.Api.UnitLibrary/Options/Tls` (namespace `Curl.Api.Options.Tls`) defines `EasyTlsOptions`, an immutable record carrying every option of the plan's `EasyTlsOptions` row - the origin's TLS options, their `PROXY_*` twins (one nested record used twice is fine) and the `DOH_SSL_*` options - each defaulting to libcurl 8.22.0's default.

## Context

ADR-0506; plan section "CURLOPT groups". Follow the rules for option-group tasks written in BL-2294's Context. Blobs (`*_BLOB`) are `ReadOnlyMemory<byte>?`; `SSLVERSION` and `SSL_OPTIONS` become enums (`CURL_SSLVERSION_*` with the `MAX` part, `CURLSSLOPT_*` flags). Defaults are libcurl's, not the Schannel build's behaviour; where the Schannel build ignores an option, say so in the doc comment (ADR-0009). BL-2304 retargets the engine onto this record.

## Acceptance criteria

- [x] `EasyTlsOptions` exists in `Curl.Api.UnitLibrary/Options/Tls` and carries every option in the plan's row, each documented with its `CURLOPT_*` name and default.
- [x] Tests in `Curl.Api.UnitTests/Options/Tls` pin every default and that a `with` copy changes only the property set.
- [x] This task's Notes list each `CURLOPT_*` of the plan's row with the property that carries it (none left without one).
- [x] `dotnet build` is clean (warnings are errors).
- [x] `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged.

## Notes

Shape: `EasyTlsOptions` (origin-only and `DOH_SSL_*` options) holds one `TlsPeerOptions` twice, `Origin` and `Proxy`, for every option libcurl has in both forms; enums `SslVersion`, `SslVersionMaximum` (values pre-shifted 16 bits, so OR-ing the two gives the option's `long`) and `[Flags] SslOptions`, all with libcurl's numeric values.

Choices (sensible defaults, no ADR needed):
- `SSL_VERIFYHOST` / `DOH_SSL_VERIFYHOST` are `bool` (default `true` = libcurl's 2): libcurl treats 1 as 2 since 7.66, so the option is on or off.
- `SSLVERSION` default is `Default` + `SslVersionMaximum.None`, the option's literal default 0.
- `CA_CACHE_TIMEOUT` is a `TimeSpan` (86400 s); libcurl's -1 maps to `Timeout.InfiniteTimeSpan`.
- `SSLENGINE_DEFAULT` is an action option in libcurl; carried as `EngineIsDefault`, default `false` (never set).
- `CAINFO`/`CAPATH` default `null` = the build's built-in default (none in the Schannel build).

CURLOPT -> property:
- `SSLCERT` / `PROXY_SSLCERT` -> `TlsPeerOptions.Certificate` (via `Origin` / `Proxy`)
- `SSLCERT_BLOB` / `PROXY_SSLCERT_BLOB` -> `TlsPeerOptions.CertificateBlob`
- `SSLCERTTYPE` / `PROXY_SSLCERTTYPE` -> `TlsPeerOptions.CertificateType`
- `SSLKEY` / `PROXY_SSLKEY` -> `TlsPeerOptions.Key`
- `SSLKEY_BLOB` / `PROXY_SSLKEY_BLOB` -> `TlsPeerOptions.KeyBlob`
- `SSLKEYTYPE` / `PROXY_SSLKEYTYPE` -> `TlsPeerOptions.KeyType`
- `KEYPASSWD` / `PROXY_KEYPASSWD` -> `TlsPeerOptions.KeyPassword`
- `SSLVERSION` / `PROXY_SSLVERSION` -> `TlsPeerOptions.SslVersion` + `TlsPeerOptions.SslVersionMaximum`
- `SSL_VERIFYPEER` / `PROXY_SSL_VERIFYPEER` -> `TlsPeerOptions.VerifyPeer`
- `SSL_VERIFYHOST` / `PROXY_SSL_VERIFYHOST` -> `TlsPeerOptions.VerifyHost`
- `CAINFO` / `PROXY_CAINFO` -> `TlsPeerOptions.CertificateAuthorityFile`
- `CAINFO_BLOB` / `PROXY_CAINFO_BLOB` -> `TlsPeerOptions.CertificateAuthorityBlob`
- `CAPATH` / `PROXY_CAPATH` -> `TlsPeerOptions.CertificateAuthorityPath`
- `CRLFILE` / `PROXY_CRLFILE` -> `TlsPeerOptions.RevocationListFile`
- `ISSUERCERT` / `PROXY_ISSUERCERT` -> `TlsPeerOptions.IssuerCertificate`
- `ISSUERCERT_BLOB` / `PROXY_ISSUERCERT_BLOB` -> `TlsPeerOptions.IssuerCertificateBlob`
- `PINNEDPUBLICKEY` / `PROXY_PINNEDPUBLICKEY` -> `TlsPeerOptions.PinnedPublicKey`
- `SSL_CIPHER_LIST` / `PROXY_SSL_CIPHER_LIST` -> `TlsPeerOptions.CipherList`
- `TLS13_CIPHERS` / `PROXY_TLS13_CIPHERS` -> `TlsPeerOptions.Tls13Ciphers`
- `SSL_OPTIONS` / `PROXY_SSL_OPTIONS` -> `TlsPeerOptions.SslOptions`
- `SSLENGINE` -> `EasyTlsOptions.Engine`
- `SSLENGINE_DEFAULT` -> `EasyTlsOptions.EngineIsDefault`
- `SSL_VERIFYSTATUS` -> `EasyTlsOptions.VerifyStatus`
- `CA_CACHE_TIMEOUT` -> `EasyTlsOptions.CertificateAuthorityCacheTimeout`
- `CERTINFO` -> `EasyTlsOptions.GatherCertificateInfo`
- `SSL_EC_CURVES` -> `EasyTlsOptions.EllipticCurves`
- `SSL_SIGNATURE_ALGORITHMS` -> `EasyTlsOptions.SignatureAlgorithms`
- `SSL_SESSIONID_CACHE` -> `EasyTlsOptions.SessionIdCache`
- `SSL_FALSESTART` -> `EasyTlsOptions.FalseStart`
- `ECH` -> `EasyTlsOptions.EncryptedClientHello`
- `DOH_SSL_VERIFYPEER` -> `EasyTlsOptions.DohVerifyPeer`
- `DOH_SSL_VERIFYHOST` -> `EasyTlsOptions.DohVerifyHost`
- `DOH_SSL_VERIFYSTATUS` -> `EasyTlsOptions.DohVerifyStatus`

Measured: `Measure-CodeQuality.ps1 -Library Curl.Api.UnitLibrary` -> 100% line, 100% branch, 0 failing members, worst CRAP 1. Curl.Api.UnitTests: 17 tests pass; full fast run green.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. EasyTlsOptions (with TlsPeerOptions for origin and proxy) carries every TLS CURLOPT with libcurl 8.22.0's defaults
