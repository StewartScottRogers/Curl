# ADR-0537 — A `pkcs11:` certificate or key without a type is `ENG`

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2349.

## Context

ADR-0530 left `TlsPeerOptions.CertificateType` and `KeyType` unset without `--cert-type`/`--key-type`
and dropped ADR-0514's `ENG` guess for a `pkcs11:` value until real curl was measured. Measured
2026-10-15 against curl 8.21.0 (Schannel, Windows), curl 8.18.0 with LibreSSL 4.2.1 (curl.se's
Windows build) and curl 8.18.0 with OpenSSL 3.5.5 (Ubuntu, in WSL), against a loopback TLS server:

- `--libcurl` shows every build's tool setting `CURLOPT_SSLCERTTYPE` `ENG` for `-E pkcs11:...`
  (any case of `pkcs11:`), `CURLOPT_SSLKEYTYPE` `ENG` for `--key pkcs11:...`, and the
  `CURLOPT_PROXY_*` twins for `--proxy-cert`/`--proxy-key`; each only for its own value, and a given
  `--cert-type`/`--key-type` is kept.
- Schannel: exit 58 `schannel: Failed to get certificate location or file for <value>` - the file
  is opened before the type is looked at, so `ENG` changes nothing.
- OpenSSL 3.5.5: a `pkcs11:` certificate or key of type `ENG` (or `PROV`) makes curl load the
  `pkcs11` provider; without one installed, exit 58
  `Failed to initialize provider: error:12800067:DSO support routines::could not load the shared library`.
  `--cert-type ENG` on a file is still `crypto engine not set, cannot load certificate`.
- LibreSSL: exit 58 `SSL_FILETYPE_ENGINE not supported for certificate`.

## Decision

- `EasyOptionsFromCommandLine.TlsOf` sets `CertificateType`/`KeyType` (origin and proxy) to `ENG` when
  the type is not given and the value is a `pkcs11:` URI (`ClientCertificateArgument.IsPkcs11Uri`),
  as curl's tool does; otherwise they stay unset (ADR-0530).
- `ClientCertificateLoader`'s OpenSSL path answers a `pkcs11:` certificate or key of type `ENG` or
  `PROV` with exit 58 and `TlsFailureMessages.OpenSslPkcs11ProviderNotLoaded`, the OpenSSL 3 text of a
  system without the pkcs11 provider, which Curl never has. The Schannel path is unchanged.
- The LibreSSL text is not matched: that loader path stands for both OpenSSL-API builds and already
  uses the OpenSSL 3 texts for `--cert-type ENG`; the OpenSSL build is the platform curl off Windows.

## Consequences

`-E pkcs11:...` behaves as each platform's curl does on a machine without a PKCS#11 module. A
machine with the pkcs11 provider installed would let real curl go further; Curl has no provider to load.
