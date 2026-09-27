---
id: BL-368
title: Match the Schannel build's exit 35 and exit 60 text for expired, incomplete and revocation-unknown chains
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-150]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-27
completed:
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

- [ ] Named tests in `Curl.Networking.UnitTests` pin each measured line above for the Schannel build, with its exit code.
- [ ] The revocation decision is recorded in an ADR, and a test pins the chosen behaviour.
- [ ] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean and `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` passes, with `TlsFailureMessages` at 100% line and branch coverage.

## Notes

- Filed by BL-150.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
