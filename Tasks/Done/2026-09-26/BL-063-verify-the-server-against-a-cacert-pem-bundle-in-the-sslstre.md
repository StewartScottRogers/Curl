---
id: BL-063
title: Verify the server against a --cacert PEM bundle in the SslStream TLS provider
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-062]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-063 — Verify the server against a --cacert PEM bundle in the SslStream TLS provider

## Goal

With `TlsClientOptions.CaCertificateFile` set, `SslStreamTlsProvider` verifies the
server's chain against the certificates in that PEM file instead of the system store,
still checks the host name, and fails with curl's exit code when the file cannot be used.

## Context

- BL-062 adds `SslStreamTlsProvider` and `TlsClientOptions`; this task adds
  `string? CaCertificateFile` to the options.
- Upstream (<https://curl.se/docs/manpage.html>, as published for curl 8.23.0 on
  2026-09-26): `--cacert` "Use the specified certificate file to verify the peer. The
  file may contain multiple CA certificates. The certificate(s) must be in PEM format."
- Measured 2026-09-26 with the local curl 8.21.0 (x86_64-w64-mingw32, Schannel,
  Release-Date 2026-06-24) against `openssl s_server` on loopback, self-signed
  `CN=localhost` certificate `cert.pem`:
  - `curl --cacert cert.pem https://localhost:18443/`: exit 0, body delivered;
  - `curl --cacert cert.pem https://127.0.0.1:18443/`: exit 60 (the name still counts);
  - `curl --cacert nonexist.pem …` (and `--cacert ''`): exit 2 before any connection,
    refused by the command-line layer. That refusal is BL-067's, not this task's.
  - `curl -sS --cacert . https://localhost:18446/` (a directory, which exists): exit 77,
    `curl: (77) schannel: failed to open CA file '.'`.
- BCL: `X509Certificate2Collection.ImportFromPemFile`, then
  `X509ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust` with the certificates
  in `CustomTrustStore`, evaluated in the `SslStream` validation callback.
- A file that exists but holds no usable certificate was not measured. Exit 77
  (`CURLE_SSL_CACERT_BADFILE`, "Problem with reading the SSL CA cert (path? access
  rights?)", <https://curl.se/libcurl/c/libcurl-errors.html>) is the candidate; measure
  it with the local curl before pinning it.
- Tests use the in-memory handshake harness BL-062 adds in `Curl.Networking.UnitTests`,
  and write PEM files to a per-test temporary directory.

## Acceptance criteria

- [x] `TlsClientOptions` has `CaCertificateFile`; `null` keeps BL-062's behaviour.
- [x] A named test asserts success when the file holds the server's self-signed
      certificate and the host name matches.
- [x] A named test asserts exit 60 when the file holds that certificate but the host
      name does not match.
- [x] A named test asserts exit 60 when the file holds a different, unrelated
      certificate authority.
- [x] A named test asserts that `Insecure = true` still succeeds whatever the file holds.
- [x] A named test asserts exit 77 (`CurlExitCode.SslCacertBadfile`) when
      `CaCertificateFile` names a directory.
- [x] curl 8.21.0's exit code and stderr for `--cacert` naming an existing empty file,
      and a file of non-PEM text, are measured and recorded in `Notes`, and a named test
      pins each exit code.
- [x] No test is tagged `Integration` and no test opens a socket.
- [x] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` is
      green.

## Notes

Message text follows BL-064; assert exit codes here.

Measured 2026-09-26 with curl 8.21.0 (Schannel) against `openssl s_server` on
loopback, self-signed `CN=localhost` certificate, `curl -sS --cacert <file> https://localhost:18463/`:

| `--cacert` | exit | stderr |
| --- | --- | --- |
| the server certificate | 0 | |
| an existing empty file | 60 | `curl: (60) schannel: the certificate or certificate chain is based on an untrusted root` plus the sslcerts.html paragraph |
| a file of non-PEM text | 60 | the same as the empty file |
| a `CERTIFICATE` block whose body is not base64 | 77 | `curl: (77) schannel: failed to extract certificate from CA file 'bad.pem': Cannot find the requested object.` |
| `.` (a directory) | 77 | `curl: (77) schannel: failed to open CA file '.'` |
| any of the above with `-k` | 0 | (the file is never read) |

Pinned by `..._WithEmptyCaCertificateFile_FailsWithPeerFailedVerification`,
`..._WithCaCertificateFileOfNonPemText_FailsWithPeerFailedVerification`,
`..._WithCaCertificateFileHoldingACorruptCertificateBlock_FailsWithSslCacertBadfile`,
`..._WithCaCertificateFileNamingADirectory_FailsWithSslCacertBadfile` and
`..._WithAnyCaCertificateFileWhenInsecure_Succeeds` in `SslStreamTlsProviderTests`.

Choices made unattended:
- The chain is checked through `SslClientAuthenticationOptions.CertificateChainPolicy`
  (`CustomRootTrust`, the file's certificates in `CustomTrustStore`), so `SslStream`
  still reports a host-name mismatch the same way as without `--cacert`.
- Revocation is `NoCheck`, the same as BL-062's system-store path, so a private CA
  without a revocation endpoint verifies.
- The file is read before the handshake starts (curl reads it during verification);
  the exit code is the same and nothing on stdout/stderr ordering differs. With `-k`
  it is not read at all, matching curl.
- `ImportFromPem` silently skips a `CERTIFICATE` block that is not base64, where curl
  exits 77; the provider counts `-----BEGIN CERTIFICATE-----` markers against the
  certificates imported and treats a shortfall as exit 77.
- An I/O error, access denied (a directory on Windows) or a certificate that does not
  decode is exit 77; the message comes from `TlsFailureMessages.CaCertificateFileUnusable`
  for BL-064 to settle.
- Added `TlsClientOptionsTests` so the record's `with` copy and init setters are
  covered (they were uncovered before this task).

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. SslStreamTlsProvider verifies against a --cacert PEM bundle: exit 60 on name or root mismatch, exit 77 for an unreadable or corrupt file, -k ignores it
