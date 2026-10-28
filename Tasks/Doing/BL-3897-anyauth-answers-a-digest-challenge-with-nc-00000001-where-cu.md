---
id: BL-3897
title: --anyauth answers a Digest challenge with nc=00000001 where curl 8.22.0 sends nc=00000002
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Authentication.UnitLibrary/DigestAuthenticator.cs, Curl.Protocol.Http.UnitTests/AnyAuthDigestNonceCountTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3897 — --anyauth answers a Digest challenge with nc=00000001 where curl 8.22.0 sends nc=00000002

## Goal

Under `--anyauth`, Curl's answer to a Digest challenge carries the nonce count curl 8.22.0 sends, `nc=00000002`, so the request bytes on the wire match.

## Context

Found by BL-3601 (CGR-072) on 2026-10-26, measured with `Record-CurlExchange.ps1` against curl 8.22.0 Schannel (`/ucrt64/bin/curl`), responses `401 WWW-Authenticate: Digest realm="r", nonce="n", qop="auth"` then `200 ok`:

    curl -s --anyauth -u user:pass http://127.0.0.1:20500/a

Real curl's second request carries `Authorization: Digest ... nc=00000002 ...`; Curl's carries `nc=00000001`. Exit code and stdout (`ok`) match. With `--digest` both send `nc=00000001`. The same holds on each hop of a `--location-trusted` redirect (BL-3601 Notes). Find why libcurl counts on under anyauth (likely the digest output being made once while picking the scheme, `lib/http.c` `output_auth_headers` / `lib/vauth/digest.c`), check the Linux OpenSSL build gives the same, and match it. `DigestAuthenticator` says every answer to a challenge is `nc=00000001` (its doc comment, ADR-0014), so that statement changes too.

## Acceptance criteria

- [ ] `curl -s --anyauth -u user:pass http://127.0.0.1:20500/a` against the responses above sends the same `nc` value through real curl and through Curl; both results recorded in Notes.
- [ ] A focused unit test pins the anyauth Digest nonce count and fails before the fix.
- [ ] `--digest` still sends `nc=00000001` to its first challenge.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 0d2102fe ef8c4797 a0d6a6f1 779790c0.
