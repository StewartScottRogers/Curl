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
completed: 2026-09-26
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

- [x] `TlsClientOptions` has `ClientCertificate` and `PrivateKey`; `null` keeps the
      earlier behaviour.
- [x] For each format the ADR from BL-059 accepts, a named test asserts the in-memory
      server receives the client certificate.
- [x] Named tests assert exit 58 (`CurlExitCode.SslCertProblem`) for a missing
      certificate file and for a format the ADR refuses, with the ADR's message text.
- [x] The measured `certificate:password` split is recorded in `Notes` and pinned by
      named tests, including a Windows drive-letter path.
- [x] No test is tagged `Integration` and no test opens a socket.
- [x] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` is
      green.

## Notes

`--cert-type`, `--key-type` and `--pass` are not in scope (filed as BL-249).

### Measured `--cert` split (2026-09-26)

Windows: curl 8.21.0 (x86_64-w64-mingw32) Schannel, whose exit 58 message
`schannel: Failed to get certificate location or file for <file>` shows the file it split
off. Linux: curl 8.18.0 OpenSSL/3.5.5 under WSL 2 (no OpenSSL 8.21.0 available), whose
`could not load PEM client certificate from <file>` shows the same.

| `--cert` value | File (Windows) | Passphrase |
| --- | --- | --- |
| `nonexist.p12:pw` | `nonexist.p12` | `pw` |
| `colon.p12:sec:ret` | `colon.p12` | `sec:ret` (opened the file protected by `sec:ret`) |
| `nonexist.p12:` | `nonexist.p12` | none |
| `:pw` | empty | `pw` |
| `nonexist.p12\:x:pw` | `nonexist.p12:x` | `pw` (same on Linux) |
| `nonexist\\x.p12` | `nonexist\x.p12` | none |
| `nonexist\x.p12`, `nonexist.p12\` | kept as written | none |
| `C:\Users\…\nonexist.p12:pw`, `c:\nonexist.p12`, `C:/nonexist.p12:pw` | the whole drive-letter path | `pw` / none |
| `C:\\nonexist.p12:x` | `C:\nonexist.p12` | `x` |
| `C:\:x` | `C::x` | none |
| `C:nonexist.p12:pw`, `C:` | `C` | `nonexist.p12:pw` / none |
| `1:\nonexist.p12:pw` | `1` | `\nonexist.p12:pw` |
| `pkcs11:foo`, `PKCS11:foo` | not split | none |
| `C:\nonexist.pem:pw` **on Linux** | `C` | the drive-letter rule is Windows-only |

This is curl's `parse_cert_parameter`: the first unescaped colon splits, `\:` and `\\` are
escapes, any other backslash is literal, and only the Windows build keeps a drive letter's
colon (letter, colon, then `\` or `/`). Pinned in `ClientCertificateArgumentTests`.

### Measured load failures (2026-09-26)

Schannel 8.21.0, all exit 58:
- missing file or a directory: `schannel: Failed to get certificate location or file for <file>`
- empty file: `schannel: Failed to read cert file <file>`
- PEM, DER or garbage: `schannel: Failed to import cert file <file>, last error is 0x80092002`
- PKCS#12 with a wrong passphrase, or none when it has one: `schannel: Failed to import cert file <file>, password is bad`
- `--key` is ignored: a PKCS#12 `--cert` with a missing `--key` loads.

OpenSSL 8.18.0:
- missing file: exit 58, `could not load PEM client certificate from <file>, OpenSSL error error:80000002:system library::No such file or directory, (no key found, wrong passphrase, or wrong file format?)`
- directory, empty, DER, PKCS#12, key-only PEM: exit 58, same line with `error:0480006C:PEM routines::no start line`
- PEM certificate whose key does not load (no `--key` and no key in the file, missing
  `--key`, a directory, a key for another certificate, an encrypted key with a wrong or no
  passphrase): **exit 43**, `unable to set private key file: '<key file>' type PEM`.
  Exit 43 is what curl returns, so Curl returns it too (`CurlExitCode.BadFunctionArgument`).
- `cert.pem:secret --key enckey.pem` (PKCS#8 encrypted) and `cert.pem: --key key.pem` load.

### Decisions (defaults taken, unattended run)

- The split is `ClientCertificateArgument.Split`, with the drive-letter rule tied to the
  build (Schannel = Windows curl), so the provider's internal build switch drives it.
- A client certificate is loaded before `--cacert` is read, so a run where both fail
  reports exit 58. Not measured; curl's Schannel build loads the certificate when it
  acquires credentials, before any CA file is read.
- The OpenSSL build reads the first CERTIFICATE block and decrypts only a PKCS#8
  `ENCRYPTED PRIVATE KEY`; a legacy `Proc-Type: 4,ENCRYPTED` key is exit 43 (the BCL has
  no reader for it).
- A PEM-loaded key is re-imported through PKCS#12 so Schannel on Windows can sign with it;
  the certificate is presented through `ClientCertificateContext` (built offline) so it is
  sent whatever issuers the server lists, and it is disposed with the connection or on
  failure, which removes the key Windows stored for it.
- Against `openssl s_server` both curl and a TLS 1.3 handshake with the loaded PKCS#12
  gave exit 35 `SEC_E_INTERNAL_ERROR`, an interop quirk of that server, not of the load;
  ADR-0009's earlier exit 0 for PKCS#12 stands.
- The provider tests live in `SslStreamTlsProviderTests.ClientCertificate.cs`, a partial of
  `SslStreamTlsProviderTests`, keeping one test class per production class. Tests that
  give the OpenSSL build a Windows temporary path escape its drive colon (`C\:`), as a user
  of that build would.

### Gates

`Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: every member added or
changed here is at 100% line and branch coverage, complexity at most 10. The three members
it still lists (`TcpDialer.DialAsync`, `UdpDatagramChannel.SendAsync`/`ReceiveAsync`) were
already there before this task; only the Integration tests cover them.

Follow-ups: BL-248 (Schannel certificate-store `--cert`), BL-249 (`--cert-type`,
`--key-type`, `--pass`). Wiring the options from the command line is BL-072.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. SslStreamTlsProvider presents a --cert client certificate: PKCS#12 in the Schannel build, PEM with --key in the OpenSSL build, with curl's measured --cert split and exit 58/43 messages
