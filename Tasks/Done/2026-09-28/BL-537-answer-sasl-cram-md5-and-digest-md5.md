---
id: BL-537
title: Answer SASL CRAM-MD5 and DIGEST-MD5
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-536]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Record-CurlExchange.ps1, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-537 — Answer SASL CRAM-MD5 and DIGEST-MD5

## Goal

The SASL authenticator answers CRAM-MD5 and DIGEST-MD5 challenges as curl 8.21.0 does, and ranks them where curl ranks them, so a server offering them alongside PLAIN gets the mechanism curl would pick.

## Context

- Conformance audit 2026-09-28, row 34. Builds on BL-536's authenticator and ranking.
- RFC 2195 (CRAM-MD5: HMAC-MD5 of the challenge, `System.Security.Cryptography.HMACMD5`), RFC 2831 (DIGEST-MD5). `Curl.Authentication.UnitLibrary` already has HTTP Digest (`DigestAuthenticator.cs`, `DigestChallenge.cs`, `DigestClientNonce.cs`); reuse its parsing and nonce seam where it fits, without changing HTTP Digest behaviour.
- DIGEST-MD5's client nonce is random; inject it as HTTP Digest does so tests are deterministic.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1 -Smtp -SmtpReply` offering `AUTH CRAM-MD5 PLAIN` with a fixed `334` challenge, and `AUTH DIGEST-MD5` with a fixed challenge; the responses curl sent copied into Notes.
- [x] `Curl.Authentication.UnitTests` reproduce the measured CRAM-MD5 response byte for byte, and the DIGEST-MD5 response for a fixed client nonce, and pin the new ranking.
- [x] HTTP Digest tests pass unchanged.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Authentication.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- **Touches widened** (no task in Doing names either): `Record-CurlExchange.ps1`, whose
  `-Smtp` server had no DIGEST-MD5 continuation (it now sends a fixed RFC 2831 challenge,
  then an `rspauth` line); `Documentation/Planning/Decisions` for ADR-0139 and its index row.
- **Measured 2026-09-28**, curl 8.21.0 Schannel (Windows) and curl 8.18.0 OpenSSL (WSL
  Ubuntu, `-Curl wsl.exe -ListenAddress 172.26.96.1`), `-u user:pencil`:
  - `AUTH CRAM-MD5 PLAIN` offered: `AUTH CRAM-MD5`, `334 PDE4OTYuNjk3MTcwOTUyQGxvY2FsaG9zdD4=`
    (`<1896.697170952@localhost>`), curl sent `dXNlciBlZTg3NzliY2M1MzFhNzhmNGRiMzc4YzQ3N2E1N2IwZA==`
    = `user ee8779bcc531a78f4db378c477a57b0d`, 235. `-u user:` sent `user 3b1dd61d3f838d03eeeabfdde91ec5e9`.
    `--sasl-ir` changes nothing (no initial response).
  - `AUTH DIGEST-MD5 CRAM-MD5 PLAIN`, challenge
    `realm="localhost",nonce="OA6MG9tEQGm2hh",qop="auth",algorithm=md5-sess,charset=utf-8`:
    - Schannel: `username="user",realm="",nonce="OA6MG9tEQGm2hh",digest-uri="smtp/127.0.0.1",cnonce="81eed5b913007ab96776b8224946a866",nc=00000001,response=1ae34deb057c4c638fc093d4913d8f29,qop=auth,charset=utf-8`
    - OpenSSL: `username="user",realm="localhost",nonce="OA6MG9tEQGm2hh",cnonce="dab6bbea0a329577a0c691f97f35a087",nc="00000001",digest-uri="smtp/172.26.96.1",response=8c416297044dbc635e16a5432c6cc0a1,qop=auth`
    - Both then answer `334 rspauth=...` with an empty line; 235.
  - Variants (all pinned in `SaslDigestMd5Tests`): no charset, no realm, no qop, `qop="auth-int,AUTH"`,
    `algorithm=MD5-SESS`, `dom\user`, `dom/user`, `a/b\c`, `a\b/c`, `user@dom`, `usér` with and
    without charset. Schannel exits 94 (nothing sent) for no nonce, no algorithm, `qop="auth-int"`;
    OpenSSL sends `*` and exits 67 for those and for no qop.
  - Ranking: with `--oauth2-bearer` (with or without `-u`) and `DIGEST-MD5 CRAM-MD5 PLAIN` offered,
    exit 67; `--login-options AUTH=CRAM-MD5` skips DIGEST-MD5; `--sasl-authzid` is not sent.
- **Decisions** (ADR-0139): DIGEST-MD5 follows the platform's curl (SSPI format on Windows,
  curl's own elsewhere), chosen in the constructor so `Curl.Console` is unchanged. SSPI's
  exit 94 needs a wider `ISaslExchange` contract, filed as **BL-781**; until then those
  challenges answer `null` (exit 67). Every measured hash matched, which confirmed RFC 2831's
  ISO 8859-1 hashing rule under `charset=utf-8`.
- HTTP Digest code untouched except the new `DigestClientNonce.CreateRandomHex`; its tests pass.
- Tests: `Curl.Authentication.UnitTests` 324 passed; whole solution fast run green;
  `Measure-CodeQuality.ps1 -Library Curl.Authentication.UnitLibrary`: 100% line, 100% branch,
  0 failing members, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. SASL answers CRAM-MD5 and DIGEST-MD5 as curl 8.21.0 does (SSPI format on Windows, curl's own elsewhere) and ranks them above PLAIN
