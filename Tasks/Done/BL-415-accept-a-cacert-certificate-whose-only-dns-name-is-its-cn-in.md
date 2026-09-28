---
id: BL-415
title: Accept a --cacert certificate whose only DNS name is its CN in the Schannel build
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-368]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-27
completed: 2026-09-27
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

- [x] A named test in `Curl.Networking.UnitTests` runs a handshake in the Schannel build with `--cacert` against a CN=localhost certificate whose subjectAltName holds only `127.0.0.1`, target `localhost`, and gets exit 0.
- [x] A named test shows the same certificate against another host name still fails with the measured `CertGetNameString() failed to match` line.
- [x] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean, the fast tests pass, and `TlsFailureMessages` and `SslStreamTlsProvider` stay at 100% line and branch coverage.

## Notes

- Filed by BL-368.
- Delivered directly rather than through the full `/feature` stages: one private method in
  `SslStreamTlsProvider` and one new internal class, `SchannelCommonNameCheck`.
- Design (ADR-0103): with `--cacert` the Schannel build drops `RemoteCertificateNameMismatch`
  when the target is a host name, the certificate has no DNS subjectAltName, and its CN
  (`GetNameInfo(DnsName)`, which is `CertGetNameString(CERT_NAME_DNS_TYPE)` on Windows)
  matches. Applied in the validation callback and in `VerifyPeer`, so the handshake event's
  verify result agrees.
- Wildcards: a CN starting `*.` matches as curl's `Curl_cert_hostcheck` does: exactly one
  left-most label, the pattern needs at least two dots (`*.com` matches nothing), never
  an IP literal, a partial wildcard (`w*.example.com`) is compared literally, and one
  trailing dot is ignored on each side. Chosen because it is curl's own function, which
  the Schannel build calls on the name `CertGetNameString` returns.
- Found: .NET's own name check already accepts this certificate on Windows, so the
  handshake test passes there without the change; the change matters where .NET reports
  the mismatch. The `VerifyPeer` test hands the mismatch in directly so the new branch is
  covered on every platform.
- Also found: in the OpenSSL build, .NET's check on Windows accepts this certificate where
  curl's OpenSSL build refuses it. Filed as BL-460, not fixed here (the task says leave
  the OpenSSL path alone).
- `touches` widened to `Documentation/Planning/Decisions` for ADR-0103 and its index line;
  no task in `Doing` names it.
- Tests: `SchannelCommonNameCheckTests` (19 cases), and in
  `SslStreamTlsProviderTests.CommonName.cs` two handshakes and three `VerifyPeer` cases.
  Networking fast tests: 741 passed, 6 skipped. `SchannelCommonNameCheck`,
  `SslStreamTlsProvider` and `TlsFailureMessages` at 100% line and branch.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. With --cacert the Schannel build accepts a certificate with no DNS subjectAltName whose CN matches the host, as curl's Curl_cert_hostcheck matches it
