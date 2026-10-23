---
id: BL-3218
title: Report an unreachable revocation responder on Windows with the Schannel build's exit 35 text
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.cs, Curl.Networking.UnitTests/ServerCertificateRevocationTests.cs]
requirement: none
created: 2026-10-23
completed:
---
# BL-3218 — Report an unreachable revocation responder on Windows with the Schannel build's exit 35 text

## Goal

On Windows, a server whose system store chain has a revocation status that cannot be fetched (responder unreachable) fails as curl 8.22.0's Schannel build does, with its measured exit code and text, unless `--ssl-revoke-best-effort` or `--ssl-no-revoke`.

## Context

BL-3215 (ADR-0781) made the Schannel build check revocation online against the system store and pinned a revoked chain as exit 35 `CRYPT_E_REVOKED`. An unknown or offline status without best effort still falls through to `ServerCertificateVerification.SchannelPeerVerificationFailure` (exit 60 with the untrusted-root style text); Schannel is expected to fail the handshake with exit 35 and `CRYPT_E_NO_REVOCATION_CHECK (0x80092012)` or `CRYPT_E_REVOCATION_OFFLINE (0x80092013)`. Measure curl 8.22.0 (Schannel) first with `Record-CurlExchange.ps1` against a server whose certificate names an unreachable CRL/OCSP endpoint, then map it beside `SchannelRevocationRefusal`. Tests use the `ReadChainStatus` seam or a chain whose distribution points name missing files; no network.

## Acceptance criteria

- [ ] The measured curl 8.22.0 Schannel exit code and text for an unreachable revocation responder are recorded under Notes.
- [ ] `Curl.Networking.UnitTests/ServerCertificateRevocationTests.cs` (Windows `[OSCondition]`) pins that exit code and text for a system store chain, and that `--ssl-revoke-best-effort` and `--ssl-no-revoke` still accept it.
- [ ] Build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 0 failing members; no test touches the network.

## Notes

## Log

- 2026-10-23: Created.
