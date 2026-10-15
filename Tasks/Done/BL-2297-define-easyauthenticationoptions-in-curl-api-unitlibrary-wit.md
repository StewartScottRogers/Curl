---
id: BL-2297
title: Define EasyAuthenticationOptions in Curl.Api.UnitLibrary with libcurl's defaults
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-2292]
touches: [Curl.Api.UnitLibrary/Options/Authentication, Curl.Api.UnitTests/Options/Authentication]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2297 — Define EasyAuthenticationOptions in Curl.Api.UnitLibrary with libcurl's defaults

## Goal

`Curl.Api.UnitLibrary/Options/Authentication` (namespace `Curl.Api.Options.Authentication`) defines `EasyAuthenticationOptions`, an immutable record carrying every option of the plan's `EasyAuthenticationOptions` row (netrc, user and password, login options, HTTP and proxy auth bitmasks, SASL, OAuth bearer, service names, GSS-API delegation, Kerberos level, AWS SigV4, TLS-SRP), each defaulting to libcurl 8.22.0's default.

## Context

ADR-0506; plan section "CURLOPT groups". Follow the rules for option-group tasks written in BL-2294's Context. `HTTPAUTH`/`PROXYAUTH` become a `[Flags]` enum with `CURLAUTH_*`'s bit values (including `ANY`, `ANYSAFE`, `ONLY`); `NETRC` an enum of `CURL_NETRC_*`. Secrets are plain `string?` here; no logging of them anywhere (the security auditor checks secrets in output). BL-2306 retargets the engine onto this record.

## Acceptance criteria

- [x] `EasyAuthenticationOptions` exists in `Curl.Api.UnitLibrary/Options/Authentication` and carries every option in the plan's row, each documented with its `CURLOPT_*` name and default.
- [x] Tests in `Curl.Api.UnitTests/Options/Authentication` pin every default, the `CURLAUTH_*` bit values, and that a `with` copy changes only the property set.
- [x] The record's `ToString()` does not contain a password, bearer token or TLS-SRP password set on it (a test sets each and asserts its absence).
- [x] This task's Notes list each `CURLOPT_*` of the plan's row with the property that carries it (none left without one).
- [x] `dotnet build` is clean (warnings are errors).
- [x] `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged.

## Notes

Plan: one `sealed record` with `init` properties plus three enums in the same folder, following the BL-2294 rules and the sibling `EasyProxyOptions`. Defaults are from curl 8.22.0's `docs/libcurl/opts`.

Choices:
- `HttpAuthenticationMethods` is a `[Flags]` enum on `uint`. `CURLAUTH_ONLY` (1<<31), `ANY` (~DIGEST_IE = 0xFFFFFFEF) and `ANYSAFE` (0xFFFFFFEE) fit there without sign games, and they are the 32 bits a `long` carries on Windows. `NTLM_WB` (1<<5) is kept although 8.22.0 no longer supports it, because curl.h still defines it.
- `NETRC` -> `NetrcUsage` (Ignored/Optional/Required), `GSSAPI_DELEGATION` -> `GssApiDelegation` (None/PolicyFlag/Flag).
- TLS-SRP's proxy twins are flat `ProxyTlsAuth*` properties rather than a nested record: the group is three strings, so a nested type would add nothing.
- `ToString()` redaction: a hand-written `PrintMembers` prints `<redacted>` for a set `UserPassword`, `Password`, `ProxyUserPassword`, `ProxyPassword`, `XOAuth2Bearer`, `TlsAuthPassword` or `ProxyTlsAuthPassword`, and nothing for an unset one. The combined `USERPWD` strings count as secrets because they carry a password.

CURLOPT -> property:
- NETRC -> Netrc; NETRC_FILE -> NetrcFile
- USERPWD -> UserPassword; USERNAME -> UserName; PASSWORD -> Password; LOGIN_OPTIONS -> LoginOptions
- HTTPAUTH -> HttpAuthentication; PROXYAUTH -> ProxyAuthentication
- PROXYUSERPWD -> ProxyUserPassword; PROXYUSERNAME -> ProxyUserName; PROXYPASSWORD -> ProxyPassword
- SASL_AUTHZID -> SaslAuthorizationIdentity; SASL_IR -> SaslInitialResponse; XOAUTH2_BEARER -> XOAuth2Bearer
- DISALLOW_USERNAME_IN_URL -> DisallowUserNameInUrl; UNRESTRICTED_AUTH -> UnrestrictedAuthentication
- SERVICE_NAME -> ServiceName; PROXY_SERVICE_NAME -> ProxyServiceName
- GSSAPI_DELEGATION -> GssApiDelegation; KRBLEVEL -> KerberosLevel; AWS_SIGV4 -> AwsSigV4
- TLSAUTH_TYPE/USERNAME/PASSWORD -> TlsAuthType/TlsAuthUserName/TlsAuthPassword
- PROXY_TLSAUTH_TYPE/USERNAME/PASSWORD -> ProxyTlsAuthType/ProxyTlsAuthUserName/ProxyTlsAuthPassword

Measured: Measure-CodeQuality.ps1 -Library Curl.Api.UnitLibrary reported 100% line, 100% branch, 0 failing members and worst CRAP 2. Curl.Api.UnitTests ran 87 tests, all passed. The full fast run passed all 39 test assemblies. PassingUpstreamCases.txt was not touched.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. EasyAuthenticationOptions carries every CURLOPT of the authentication row with libcurl 8.22.0 defaults, CURLAUTH bit enum, and a ToString that redacts secrets
