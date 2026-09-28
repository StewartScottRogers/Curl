---
id: BL-415
title: Accept a --cacert certificate whose only DNS name is its CN in the Schannel build
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-368]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-415 — Accept a --cacert certificate whose only DNS name is its CN in the Schannel build

## Goal

In the Schannel build with `--cacert`, a server certificate whose subjectAltName holds only IP addresses and whose CN is the target host name verifies, as curl 8.21.0's Schannel build accepts it, instead of failing .NET's name check.

## Context

- Measured by BL-150 (see its Notes, `Tasks/Done/2026-09-27_1215/BL-150-*.md`): with `--cacert`, such a certificate **succeeds** in the Schannel build, because `CertGetNameString(CERT_NAME_DNS_TYPE)` falls back to the CN when the subjectAltName holds no DNS name, and curl matches the host against that. .NET's name check fails it (`SslPolicyErrors.RemoteCertificateNameMismatch`).
- The check is `SslStreamTlsProvider.VerifyPeer` in `Curl.Networking.UnitLibrary`; the Schannel name-mismatch messages are in `TlsFailureMessages.SchannelPeerFailedVerification`.
- Without `--cacert` Schannel does the name check itself (SEC_E_WRONG_PRINCIPAL); do not change that path. The OpenSSL build already falls back to the CN only when there is no DNS or IP subjectAltName; do not change that either.
- Decide wildcard handling in the CN (curl's `Curl_cert_hostcheck` allows a left-most `*` label) and record it in the task Notes.

## Acceptance criteria

- [ ] A named test in `Curl.Networking.UnitTests` runs a handshake in the Schannel build with `--cacert` against a CN=localhost certificate whose subjectAltName holds only `127.0.0.1`, target `localhost`, and gets exit 0.
- [ ] A named test shows the same certificate against another host name still fails with the measured `CertGetNameString() failed to match` line.
- [ ] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean, the fast tests pass, and `TlsFailureMessages` and `SslStreamTlsProvider` stay at 100% line and branch coverage.

## Notes

- Filed by BL-368.

## Log

- 2026-09-27: Created.
