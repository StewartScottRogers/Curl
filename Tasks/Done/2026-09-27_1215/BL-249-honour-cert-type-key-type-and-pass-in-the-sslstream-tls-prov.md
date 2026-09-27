---
id: BL-249
title: Honour --cert-type, --key-type and --pass in the SslStream TLS provider
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-065]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-249 — Honour --cert-type, --key-type and --pass in the SslStream TLS provider

## Goal

`TlsClientOptions` carries `--cert-type`, `--key-type` and `--pass`, and
`SslStreamTlsProvider` loads the client certificate and key by them as ADR-0009 section 3
decides for each build.

## Context

- BL-065 loads `--cert` with the default type only: PKCS#12 in the Schannel build, PEM
  (with `--key`) in the OpenSSL build (`ClientCertificateLoader`), and takes a passphrase
  only from `--cert <file>:<passphrase>`.
- ADR-0009 section 3: Windows accepts PKCS#12 with or without `--cert-type P12`; `--cert-type
  PEM` or `DER` is exit 58, `schannel: certificate format compatibility error for <path>`.
  Linux and macOS: PEM by default, PKCS#12 with `--cert-type P12`, DER with `--cert-type DER`.
- ADR-0009 says the OpenSSL build's text for a P12 or DER file that fails to load is
  measured before it is pinned. Measure curl's OpenSSL build (8.21.0 if available) for
  `--cert-type P12` and `DER` failures, `--key-type DER`, and `--pass` with an encrypted
  key, and record the lines in `Notes`.
- Measured by BL-065 on curl 8.18.0 OpenSSL: a key that does not load is exit 43,
  `unable to set private key file: '<key>' type PEM`; the `type PEM` part presumably
  follows `--key-type`.
- The `--cert-type`/`--key-type`/`--pass` parsing belongs to `Curl.Cli.UnitLibrary`; if it
  is not parsed yet, file that as its own task rather than widening this one.

## Acceptance criteria

- [x] `TlsClientOptions` has properties for `--cert-type`, `--key-type` and `--pass`;
      `null` keeps BL-065's behaviour.
- [x] Named tests assert, per build, the in-memory server receives the certificate for
      every type ADR-0009 accepts.
- [x] Named tests pin exit 58 and the measured message for every type ADR-0009 refuses.
- [x] A named test asserts `--pass` opens an encrypted key in the OpenSSL build and a
      protected PKCS#12 file in the Schannel build.
- [x] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` is green.

## Notes

Filed as a follow-up by BL-065.

- Delivered: `TlsClientOptions.CertificateType`, `PrivateKeyType`, `Passphrase` (null keeps
  BL-065's behaviour). `ClientCertificateFileType`/`ClientCertificateFileTypeName` read the
  type names case-insensitively (PEM, DER, P12, ENG, PROV, anything else unsupported).
  `DerCertificateFile` reads a DER certificate and reports OpenSSL's ASN.1 error for a
  file that is not one. `--pass` replaces the passphrase split from `--cert`.
- Schannel build: P12 (any case) or no type loads; every other type, once the file is found,
  is exit 58 `schannel: certificate format compatibility error for <path>`; a missing file
  keeps `Failed to get certificate location or file`. `--key`/`--key-type` ignored.
- OpenSSL build, measured on curl 8.18.0 / OpenSSL 3.5.5 (WSL), spot-rechecked 2026-09-26:
  - `--cert-type P12` wrong/no pass: 58 `could not parse PKCS12 file, check password, OpenSSL error error:11800071:PKCS12 routines::mac verify failure`
  - `--cert-type P12` on a missing file: 58 `could not open PKCS12 file '<f>'`; on a PEM/empty file or directory: 58 `error reading PKCS12 file '<f>'`
  - `--cert-type DER` bad file: 58 `could not load ASN1 client certificate from <f>, OpenSSL error <err>, (no key found, wrong passphrase, or wrong file format?)`
  - `--key-type DER` key that does not load: 43 `unable to set private key file: '<key>' type DER` (the type follows `--key-type` as given)
  - `--cert-type FOO`: 43 `not supported file type 'FOO' for certificate`; `--key-type FOO`: 43 `not supported file type for private key`
  - `--cert-type ENG`/`PROV`: 58 `crypto engine|provider not set, cannot load certificate`; same for `--key-type` with `private key`
  - `--key-type P12`: 58 `file type P12 for private key not supported`
  - `--pass` wrong for an encrypted PEM key: 43 `unable to set private key file: '<key>' type PEM`; right pass connects.
- Decision (sensible default): the exact OpenSSL error strings are pinned as measured;
  no ADR change needed, ADR-0009 section 3 already decides the per-build acceptance and
  asked only that the text be measured, which is recorded here.
- Resumed after a token cut-off: the earlier run had written all code and tests; this run
  fixed one CS0051 (internal enum as a public test parameter) and verified.
- Coverage: every new/changed line and branch in the library is covered; the one
  uncovered line in `SslStreamTlsProvider.cs` (272) is the pre-existing Linux-only cipher path.
- Follow-up filed: BL-286, parse `--cert-type`, `--key-type`, `--pass` in Curl.Cli and map them.
- Tests: Curl.Networking.UnitTests 357 passed, 6 skipped (pre-existing OpenSSL-on-Linux cases).

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. SslStreamTlsProvider loads client certificates by --cert-type, --key-type and --pass per ADR-0009 for both builds
