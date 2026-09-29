---
id: BL-610
title: Honour --cert-status and --ssl-auto-client-cert on every platform
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-607, BL-705, BL-708]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed:
---
# BL-610 — Honour --cert-status and --ssl-auto-client-cert on every platform

## Goal

`--cert-status` checks the server's stapled OCSP response and fails with exit 91 `CURLE_SSL_INVALIDCERTSTATUS` and curl 8.21.0's message when it is missing, revoked or invalid, and `--ssl-auto-client-cert` presents a client certificate picked from the user's certificate store when the server asks for one, both on Windows, Linux and macOS; and whether any Curl option can produce exit 83 (`CURLE_SSL_ISSUER_ERROR`) is measured and recorded.

## Context

- Conformance audit 2026-09-28, row 17 (Major; exits 83 and 91 never produced). Option: BL-607.
- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): if any official curl build supports a feature, Curl supports it on every platform, output text matching the platform's curl where both do the same thing. OpenSSL builds check the stapled response; `SslStream` exposes none, so `--cert-status` routes the transfer through the hand-built TLS client (BL-705 verifies the response; BL-708 routes; add the routing row). `--ssl-auto-client-cert` is Schannel's (`SslClientAuthenticationOptions.LocalCertificateSelectionCallback` over `X509Store(StoreName.My, StoreLocation.CurrentUser)` on Windows); off Windows the same store API reaches .NET's user store (and the keychain on macOS) and the option does the same there.
- Record how each is honoured per platform in an ADR marked "Decided by Claude under Stewart's delegation" (HOW, not WHETHER), with the measured text each platform matches.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1 -Tls -k` (and `-NoServer` against `openssl s_server -status_file <resp>` for stapling): `--cert-status` with a good, a revoked and no stapled response, and `--ssl-auto-client-cert` against a server requesting a client certificate, on Windows and on Linux or macOS, and with curl.se's official Windows build; stderr and exit code copied into Notes, plus a note on exit 83 (the CLI has no issuer-certificate option in 8.21.0 if that is what the manual shows).
- [ ] Tests pin, on every platform, a transfer passing with a good stapled response and failing with exit 91 and the measured message for revoked and missing ones, and an automatically chosen client certificate presented from a fake store; where platforms' texts differ, each is pinned in its own `OSCondition` test.
- [ ] The ADR exists in `Documentation/Planning/Decisions` (number checked unused) and is indexed in its `README.md`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Lane 1 could not integrate: fast tests failed after rebasing onto the other lanes' work. The work is on branch factory/BL-610-lane-1-20260928-212155; start with git cherry-pick --no-commit factory/BL-610-lane-1-20260928-212155 and fix it.
