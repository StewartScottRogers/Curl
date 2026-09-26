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
completed:
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

- [ ] `TlsClientOptions` has `CaCertificateFile`; `null` keeps BL-062's behaviour.
- [ ] A named test asserts success when the file holds the server's self-signed
      certificate and the host name matches.
- [ ] A named test asserts exit 60 when the file holds that certificate but the host
      name does not match.
- [ ] A named test asserts exit 60 when the file holds a different, unrelated
      certificate authority.
- [ ] A named test asserts that `Insecure = true` still succeeds whatever the file holds.
- [ ] A named test asserts exit 77 (`CurlExitCode.SslCacertBadfile`) when
      `CaCertificateFile` names a directory.
- [ ] curl 8.21.0's exit code and stderr for `--cacert` naming an existing empty file,
      and a file of non-PEM text, are measured and recorded in `Notes`, and a named test
      pins each exit code.
- [ ] No test is tagged `Integration` and no test opens a socket.
- [ ] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` is
      green.

## Notes

Message text follows BL-064; assert exit codes here.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
