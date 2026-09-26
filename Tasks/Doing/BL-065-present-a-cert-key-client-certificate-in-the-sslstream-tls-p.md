---
id: BL-065
title: Present a --cert/--key client certificate in the SslStream TLS provider
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-059, BL-062]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-065 — Present a --cert/--key client certificate in the SslStream TLS provider

## Goal

With `TlsClientOptions.ClientCertificate` (and optionally `PrivateKey`) set,
`SslStreamTlsProvider` loads the client certificate in the formats the ADR from BL-059
accepts, presents it when the server asks, and fails with exit 58 when it cannot be
loaded.

## Context

- BL-062 adds `SslStreamTlsProvider` and `TlsClientOptions`; this task adds
  `string? ClientCertificate` (the `-E`/`--cert` value verbatim, as BL-067 parses it) and
  `string? PrivateKey` (the `--key` value).
- Upstream (<https://curl.se/docs/manpage.html>, as published for curl 8.23.0 on
  2026-09-26): `--cert` "Use the specified client certificate file … The certificate
  must be PEM format"; `--key` "Private key file name. Allows you to provide your private
  key in a separate file." `--cert` also accepts `certificate:password`; measure how
  curl 8.21.0 splits that value when the path itself holds a colon (for example a
  Windows drive letter) before implementing the split, and record the result in `Notes`.
- Measured 2026-09-26 with the local curl 8.21.0 (x86_64-w64-mingw32, Schannel,
  Release-Date 2026-06-24), `-sS -k` against `openssl s_server` on loopback:
  `--cert cert.pem --key key.pem` (PEM) is exit 58,
  `schannel: Failed to import cert file cert.pem, last error is 0x80092002`;
  `--cert nonexist.pem` is exit 58,
  `schannel: Failed to get certificate location or file for nonexist.pem`. The Schannel
  build therefore refuses PEM, contrary to the manpage; which formats Curl accepts, and
  which message text it prints, is BL-059's decision.
- Exit 58 is `CURLE_SSL_CERTPROBLEM`, "problem with the local client certificate"
  (<https://curl.se/libcurl/c/libcurl-errors.html>); `CurlExitCode.SslCertProblem`.
- BCL: `X509Certificate2.CreateFromPemFile(certPath, keyPath)` for PEM,
  `X509CertificateLoader.LoadPkcs12FromFile` for PKCS#12, supplied through
  `SslClientAuthenticationOptions.ClientCertificates` or a local certificate selection
  callback. On Windows, a PEM-loaded key is ephemeral and Schannel may refuse to use it;
  re-importing through PKCS#12 export is the usual remedy.
- Tests use BL-062's in-memory handshake harness with a server that requires a client
  certificate, and write certificate files to a per-test temporary directory.

## Acceptance criteria

- [ ] `TlsClientOptions` has `ClientCertificate` and `PrivateKey`; `null` keeps the
      earlier behaviour.
- [ ] For each format the ADR from BL-059 accepts, a named test asserts the in-memory
      server receives the client certificate.
- [ ] Named tests assert exit 58 (`CurlExitCode.SslCertProblem`) for a missing
      certificate file and for a format the ADR refuses, with the ADR's message text.
- [ ] The measured `certificate:password` split is recorded in `Notes` and pinned by
      named tests, including a Windows drive-letter path.
- [ ] No test is tagged `Integration` and no test opens a socket.
- [ ] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` is
      green.

## Notes

`--cert-type`, `--key-type` and `--pass` are not in scope.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
