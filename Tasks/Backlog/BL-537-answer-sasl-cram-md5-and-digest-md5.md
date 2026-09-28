---
id: BL-537
title: Answer SASL CRAM-MD5 and DIGEST-MD5
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-536]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-537 — Answer SASL CRAM-MD5 and DIGEST-MD5

## Goal

The SASL authenticator answers CRAM-MD5 and DIGEST-MD5 challenges as curl 8.21.0 does, and ranks them where curl ranks them, so a server offering them alongside PLAIN gets the mechanism curl would pick.

## Context

- Conformance audit 2026-09-28, row 34. Builds on BL-536's authenticator and ranking.
- RFC 2195 (CRAM-MD5: HMAC-MD5 of the challenge, `System.Security.Cryptography.HMACMD5`), RFC 2831 (DIGEST-MD5). `Curl.Authentication.UnitLibrary` already has HTTP Digest (`DigestAuthenticator.cs`, `DigestChallenge.cs`, `DigestClientNonce.cs`); reuse its parsing and nonce seam where it fits, without changing HTTP Digest behaviour.
- DIGEST-MD5's client nonce is random; inject it as HTTP Digest does so tests are deterministic.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1 -Smtp -SmtpReply` offering `AUTH CRAM-MD5 PLAIN` with a fixed `334` challenge, and `AUTH DIGEST-MD5` with a fixed challenge; the responses curl sent copied into Notes.
- [ ] `Curl.Authentication.UnitTests` reproduce the measured CRAM-MD5 response byte for byte, and the DIGEST-MD5 response for a fixed client nonce, and pin the new ranking.
- [ ] HTTP Digest tests pass unchanged.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Authentication.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
