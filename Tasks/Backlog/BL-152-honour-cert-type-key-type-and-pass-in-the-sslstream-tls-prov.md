---
id: BL-152
title: Honour --cert-type, --key-type and --pass in the SslStream TLS provider
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-065]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-152 — Honour --cert-type, --key-type and --pass in the SslStream TLS provider

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

- [ ] `TlsClientOptions` has properties for `--cert-type`, `--key-type` and `--pass`;
      `null` keeps BL-065's behaviour.
- [ ] Named tests assert, per build, the in-memory server receives the certificate for
      every type ADR-0009 accepts.
- [ ] Named tests pin exit 58 and the measured message for every type ADR-0009 refuses.
- [ ] A named test asserts `--pass` opens an encrypted key in the OpenSSL build and a
      protected PKCS#12 file in the Schannel build.
- [ ] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` is green.

## Notes

Filed as a follow-up by BL-065.

## Log

- 2026-09-26: Created.
