---
id: BL-368
title: Match the Schannel build's exit 35 and exit 60 text for expired, incomplete and revocation-unknown chains
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-150]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-368 — Match the Schannel build's exit 35 and exit 60 text for expired, incomplete and revocation-unknown chains

## Goal

In the Schannel build, `SslStreamTlsProvider` reports an expired, not-yet-valid or incomplete chain, and a chain whose revocation status is unknown, with the exit code and text curl 8.21.0's Schannel build prints, instead of the untrusted-root lines it prints for every chain failure today.

## Context

- BL-150 measured these on 2026-09-27 against curl 8.21.0 (x86_64-w64-mingw32) Schannel, `/mingw64/bin/curl -sS`, with `openssl s_server` on loopback and certificates signed by a private root `root.pem`, and against badssl.com. Its Notes hold the commands.
- Without `--cacert` (the system store):
  - `https://expired.badssl.com/` is exit **35**, not 60: `schannel: next InitializeSecurityContext failed: SEC_E_CERT_EXPIRED (0x80090328) - The received certificate has expired.`
  - `https://incomplete-chain.badssl.com/` succeeds (Windows fetches the missing intermediate).
- With `--cacert root.pem`:
  - an expired or not-yet-valid leaf: exit 60 `schannel: this certificate or one of the certificates in the certificate chain is not time valid`
  - the intermediate not sent: exit 60 `schannel: the certificate chain is incomplete`
  - `--cacert root.pem` against `https://example.com/`: exit 60 `schannel: the certificate chain is incomplete`
  - a valid leaf whose CA publishes no revocation endpoint: exit 60 `schannel: the revocation status is unknown`; `--ssl-no-revoke` makes it succeed. Today `SslStreamTlsProvider` sets `X509RevocationMode.NoCheck` with `--cacert`, so it succeeds where curl fails. Decide (ADR, decided by Claude under Stewart's delegation) whether to reproduce the revocation failure; `--ssl-no-revoke` may not be parsed yet.
- Also measured: with `--cacert`, a certificate whose subjectAltName holds only IP addresses and whose CN is the target host name **succeeds** in the Schannel build (CertGetNameString falls back to the CN); .NET's name check fails it. Include it or file it separately.
- The messages live in `Curl.Networking.UnitLibrary/TlsFailureMessages.cs` (`SchannelPeerFailedVerification`); the exit code is chosen in `SslStreamTlsProvider.AuthenticateAsClientAsync`.
- Do not edit ADR-0009.

## Acceptance criteria

- [x] Named tests in `Curl.Networking.UnitTests` pin each measured line above for the Schannel build, with its exit code.
- [x] The revocation decision is recorded in an ADR, and a test pins the chosen behaviour.
- [x] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean and `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` passes, with `TlsFailureMessages` at 100% line and branch coverage.

## Notes

- Filed by BL-150.
- Added `Documentation/Planning/Decisions` to `touches` for ADR-0086; no task in Doing names it.
- Delivered directly (small change in one library): `TlsFailureMessages.SchannelPeerFailedVerification` with `--cacert` now names the first of NotTimeValid, PartialChain, UntrustedRoot, RevocationStatusUnknown (curl's `schannel_verify.c` order); anything else, or no chain, stays the untrusted-root line.
- Without `--cacert`, a chain whose only problem is NotTimeValid (and no name mismatch) is exit 35 `SEC_E_CERT_EXPIRED` (`TlsFailureMessages.IsSchannelCertificateExpired`, `SchannelCertificateExpired`). Choice: only when time is the sole fault, so an untrusted self-signed expired certificate stays exit 60 untrusted root (not measured; the conservative reading). Schannel uses SEC_E_CERT_EXPIRED for not-yet-valid too. `VerifyPeer` now returns `(CurlExitCode, string)?` so the exit code can be 35.
- Revocation decided in ADR-0086 (Decided by Claude under Stewart's delegation): the Schannel build checks revocation (`X509RevocationMode.Online`, root excluded) for a `--cacert` chain; new `TlsClientOptions.SkipRevocationCheck` is `--ssl-no-revoke`. The OpenSSL build never checks.
- Not pinned by a handshake test: `--cacert root.pem` against example.com (needs the network; .NET may report UntrustedRoot rather than PartialChain there) and the system-store expired case (the system store cannot trust a test root), which is pinned through `VerifyPeer` with a built chain instead.
- CN fallback for an IP-only subjectAltName filed separately as BL-415; parsing `--ssl-no-revoke` filed as BL-414.
- Tests: Curl.Networking.UnitTests 666 passed, 6 skipped; `TlsFailureMessages`, `SslStreamTlsProvider` and `TlsClientOptions` at 100% line and branch. Whole solution build clean, fast tests green.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. Schannel build reports not-time-valid, incomplete and revocation-unknown --cacert chains with curl's exit 60 text, and an expired system-store certificate as exit 35 SEC_E_CERT_EXPIRED
