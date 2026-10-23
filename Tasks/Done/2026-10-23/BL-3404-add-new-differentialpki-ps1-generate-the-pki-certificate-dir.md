---
id: BL-3404
title: Add New-DifferentialPki.ps1: generate the pki certificate directory the TLS differential cases need, with no interaction
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [New-DifferentialPki.ps1]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3404 — Add New-DifferentialPki.ps1: generate the pki certificate directory the TLS differential cases need, with no interaction

## Goal

`New-DifferentialPki.ps1` at the repository root writes, into a directory it is given, every certificate and key file the TLS differential cases name under `{PKI}`, so `CURL_DIFFERENTIAL_PKI` can be set on any machine without the tester's files.

## Context

Stewart (2026-10-23) wants the differential tests (Curl.Conformance.Differential.IntegrationTests) to run instead of skip. `CliTlsDifferentialTests` is Inconclusive unless `CURL_DIFFERENTIAL_PKI` names "the tester's `pki` directory", which only the 2026-10-22 tester has. The cases (`Curl.Conformance.Differential.UnitLibrary/Cases/CliTls/CliTlsCases.json`) use `{PKI}/ca.pem`, `client.key`, `client.p12`, `client.pem`, `server.pem` and a missing `nope.pem`/`nodir`; `DifferentialTlsEndpoints` (same library, `Tls/`) serves the certificates `server`, `wrong`, `expired` and `rsa` (a `<name>.pem` and `<name>.key` each) with `ca.pem` as the client-certificate authority for the `MTLS` endpoint. Read `DifferentialTlsLoopbackServer` for exactly how it loads `<name>.pem` and `<name>.key`, and read `CliTlsCases.json` for what each certificate must be (server for `127.0.0.1` and `localhost`, `wrong` for another host name, `expired` already expired, `rsa` an RSA key where the others use ECDSA, the client certificate signed by `ca.pem` and also as a PKCS#12 file). Generate with `System.Security.Cryptography.X509Certificates.CertificateRequest` from PowerShell (BCL only, no openssl, no package, no prompt; it must run in Windows PowerShell 5.1 and PowerShell 7). Do not touch `Gap/`.

## Acceptance criteria

- [x] `New-DifferentialPki.ps1 -Directory <dir>` creates `ca.pem`, `server.pem`/`.key`, `wrong.pem`/`.key`, `expired.pem`/`.key`, `rsa.pem`/`.key`, `client.pem`/`.key` and `client.p12` there, asks nothing, is safe to run twice (overwrites), and its comment-based help states what each file is for.
- [x] With `CURL_DIFFERENTIAL_PKI` set to that directory and `CURL_DIFFERENTIAL_REFERENCE=/ucrt64/bin/curl.exe`'s Windows path, `dotnet test Curl.Conformance.Differential.IntegrationTests --filter "FullyQualifiedName~CliTlsDifferentialTests"` is no longer Inconclusive for a missing certificate (it runs and writes its `### DIFF` report); state the outcome in Notes.
- [x] The script writes no private key outside the directory it is given.

## Notes

- Keys are created as named CNG keys (a null name fails "Object already exists" in PowerShell) and deleted in a finally block, so none persists. Certificates are signed through X509SignatureGenerator because the RSA server certificate has a different algorithm from the ECDSA CA. Ran the script twice in Windows PowerShell 5.1 and PowerShell 7: both wrote all 12 files.
- Outcome: with CURL_DIFFERENTIAL_PKI set to a generated directory and CURL_DIFFERENTIAL_REFERENCE=C:Program FilesGitCrt64binrl.exe, CliTlsDifferentialTests passed (not Inconclusive; ran 49 s, 1 passed). The `/ucrt64/bin` path resolves to that Windows path on this machine.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 63762334 7522486b f2aeb944.
- 2026-10-23: Doing -> Done. New-DifferentialPki.ps1 generates the TLS differential pki directory; CliTlsDifferentialTests runs instead of skipping
