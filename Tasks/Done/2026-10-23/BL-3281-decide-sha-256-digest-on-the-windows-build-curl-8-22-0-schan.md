---
id: BL-3281
title: Decide SHA-256 Digest on the Windows build: curl 8.22.0 Schannel fails it with exit 94 (differential auth-digest256)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitLibrary/DigestAuthenticator.cs, Curl.Authentication.UnitLibrary/DigestAlgorithm.cs, Curl.Authentication.UnitTests]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3281 — Decide SHA-256 Digest on the Windows build: curl 8.22.0 Schannel fails it with exit 94 (differential auth-digest256)

## Goal

Curl's answer to a SHA-256 Digest challenge on Windows matches the platform's curl, decided in an ADR.

## Context

BL-3233 differential case auth-digest256 (-sS --digest -u u:p {U}/digest256): curl 8.22.0 (Schannel, SSPI digest) sends one request and exits 94 "An authentication function returned an error"; Curl answers the challenge and gets "sha256 ok". Measure whether the Windows inbox curl.exe does the same, then decide (match the platform's curl) and record an ADR; the OpenSSL builds on Linux and macOS answer SHA-256.

## Acceptance criteria

- [x] An ADR records the decision; Curl's exit, stdout and wire for auth-digest256 on Windows match it, pinned by an OSCondition unit test, with the other platforms' answer pinned separately.
- [x] The fast tests pass and Measure-CodeQuality.ps1 reports 0 failing members for each changed library.

## Notes

- Measured with Record-CurlExchange.ps1 (`-sS --digest -u u:p`): Windows inbox curl 8.21.0 (Schannel) and ucrt64 curl 8.22.0 (Schannel) both send one request and exit 94 ("An authentication function returned an error", stdout empty) for auth-digest256's SHA-256 + MD5 challenge pair. Inbox curl does the same for SHA-256 alone, sha-256, SHA-256-sess, SHA-512-256 and SHA-512-256-sess, and answers MD5 and MD5-sess.
- Decision (ADR-0791): where DigestAuthenticator matches the SSPI build (`matchesSspiBuild`, Windows), a first challenge naming a SHA algorithm throws HttpAuthenticationFailedException with exit 94. DigestAlgorithm.IsAnsweredBySspi marks MD5 and MD5-sess. Off Windows nothing changes: SHA-256 is answered.
- Tests: DigestAuthenticatorTests pins Windows (OSCondition Windows, exit 94) and the other platforms (OSCondition Exclude Windows, the SHA-256 answer to the digest256 challenges) separately. Platform-neutral tests with the flag set explicitly cover the rest. The change is platform-neutral, which covers CI on Linux and macOS. The fast tests are green here: 45 assemblies, 0 failed.
- Measure-CodeQuality.ps1 -Library Curl.Authentication.UnitLibrary: 0 failing members.
- I could not run the built Curl end to end because the lane may not read bin/. Exit 94 reaches the transfer through HttpProtocolHandler's catch of HttpAuthenticationFailedException, the same path NTLM's SSPI failure already takes. The 401 body is not written, as with NTLM.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: f344be36 69edf767.
- 2026-10-23: Doing -> Done. On Windows a SHA-256/SHA-512-256 Digest challenge fails with exit 94 as curl's Schannel build does (ADR-0791); other platforms still answer SHA-256
