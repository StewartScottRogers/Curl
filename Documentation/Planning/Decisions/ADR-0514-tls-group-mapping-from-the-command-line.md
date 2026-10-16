# ADR-0514 — Mapping the command line's TLS group onto `EasyTlsOptions`

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2304.

## Context

ADR-0506 has `Curl.Console/EasyOptionsFromCommandLine.cs` map a parsed command line onto the easy
option groups as the curl tool's `config2setopts.c` does; ADR-0513 settled the network group. BL-2304
adds the TLS group, `EasyTlsOptions`. `--ech` has three forms the record held in one string, `-E`
carries its pass phrase inside the value, and a few lines `--libcurl` writes disagree with what the
command line does today.

## Decision

- `EasyTlsOptions` gains `EncryptedClientHelloPublicName` and `EncryptedClientHelloConfigList`.
  libcurl's `CURLOPT_ECH` takes a mode (`false`, `grease`, `true`, `hard`), `pn:<name>` or
  `ecl:<list>`, and keeps each beside the others, so one string member could not hold a command line
  that gives a mode and a public name. `EncryptedClientHello` stays the mode; the two new members hold
  the name and the list without their prefixes, as `CommandLineOptions` holds them.
- `-E` / `--proxy-cert` is split into `Certificate` and `KeyPassword` in the mapping, as curl's tool
  splits it before `CURLOPT_SSLCERT` and `CURLOPT_KEYPASSWD`, with `Curl.Networking`'s
  `ClientCertificateArgument` (made public for it, so one splitter serves both): only the Windows
  build keeps a drive letter's colon, and `--pass` outranks the value's own pass phrase. A `pkcs11:`
  certificate or key with no type given is `ENG`, as `--libcurl` writes it.
- No minimum TLS version leaves `SslVersion.Default`. `--libcurl` writes `CURL_SSLVERSION_TLSv1_2`
  then, but the engine hands the TLS library its own minimum (`TlsVersion.SystemDefault`), and the
  command line's behaviour wins (ADR-0506's retarget rule). `--proxy-tlsv1` is `SslVersion.Tls1`, as
  `--libcurl` writes it; `--tls-max` and the origin's minimum never reach the proxy (BL-502).
- `--capath` reaches the proxy without `--proxy-capath`, as the engine does today; `-k` and
  `--cacert` never do (ADR-0061).
- `-w` turns `GatherCertificateInfo` on, as `--libcurl` writes `CURLOPT_CERTINFO` for any `-w`.
- `--engine` sets `Engine` only; `EngineIsDefault` stays at its default, as `--libcurl` writes no
  `CURLOPT_SSLENGINE_DEFAULT` and the build has no crypto engines (`NoCryptoEngines`).
- `--sslv2`, `--sslv3` and `--false-start` have no function in curl 8.22.0 and set nothing.
  `--ssl-sessions` is the tool's own session file (`curl_easy_ssls_import`), not a `curl_easy_setopt`
  option, so it is not on the record and stays the tool's (`TlsSessionFileLines`). `--no-alpn` is
  `CURLOPT_SSL_ENABLE_ALPN`, an HTTP-group option, left to that group's retarget.

## Consequences

`EasyOptionsFromCommandLine.TlsOf` builds the whole TLS group, ready for the engine's TLS classes to
read in place of `CommandLineOptions` (BL-2324). Nothing reads it yet, so no behaviour changes.
