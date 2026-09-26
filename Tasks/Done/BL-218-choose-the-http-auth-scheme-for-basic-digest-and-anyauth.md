---
id: BL-218
title: Choose the HTTP auth scheme for --basic, --digest and --anyauth
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-216, BL-217]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-218 — Choose the HTTP auth scheme for --basic, --digest and --anyauth

## Goal

The authenticator chooses among offered challenges as curl ranks them for `--basic`, `--digest` and `--anyauth`.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item A3. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] The choice among challenges matches curl's ranking (measured); unsupported schemes (for example NTLM, Negotiate) fall back as measured on curl 8.21.0.
- [x] `dotnet build Curl.Authentication.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Authentication`.

## Notes

- Plan item: A3 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- From BL-217 (ADR-0025): `DigestAuthenticator` answers the first Digest challenge whenever Digest is allowed, with no ranking; `BasicAndBearerAuthenticator` returns null when its pick is Digest. Choosing between them by libcurl's order is this task's.
- Delivered: `RankedHttpAuthenticator` (public, takes a `BasicAndBearerAuthenticator` and a `DigestAuthenticator`) picks in libcurl's order and hands Digest to the Digest answerer, everything else to Basic/Bearer. The order now lives once in internal `HttpAuthSchemeRanking.PickFirst`, shared with `BasicAndBearerAuthenticator`. Tests: `RankedHttpAuthenticatorTests` (Curl.Authentication.UnitTests 119 -> 138).
- Decision (ADR-0026, decided by Claude under Stewart's delegation): rank exactly as the reference build, which has NTLM and SPNEGO, and do not fall back. A pick of NTLM or Negotiate, or a Digest challenge curl cannot read, gets no answer, because the reference curl never answers the lower-ranked scheme in those cases (measured below).
- `touches` widened to `Documentation/Planning/Decisions` for ADR-0026 and its README row; no task in Doing named it.
- Measured 2026-09-26, curl 8.21.0 mingw (`/mingw64/bin/curl`), with `Record-CurlExchange.ps1 -Port 18218 -Connections 3 -Response 'HTTP/1.1 401 Unauthorized
Content-Length: 0
Connection: close
<challenges>
' -CurlArgs <flags>,-u,u:p,-m,10,http://127.0.0.1:18218/a`:
  - `--anyauth`, `Basic realm="r"` + `Digest realm="r", nonce="n"` (two headers, or one header with both): retry with `Authorization: Digest username="u",realm="r",nonce="n",uri="/a",response="544c035f0f40d9ebf0295157d8041f8c"` (SSPI format; ours uses curl's own format per ADR-0025, same response hash). `--basic --digest`: same.
  - `--anyauth`, Basic + NTLM, or NTLM alone: `Authorization: NTLM TlRMTVNTUAABAAAAB4IIogAAAAAAAAAAAAAAAAAAAAAKAPRlAAAADw==`, never Basic (exit 28, the server closes the connection NTLM needs).
  - `--anyauth`, Negotiate + Basic, or Negotiate + Digest: one retry with no `Authorization` header, then exit 0; never Basic or Digest.
  - `--anyauth`, `Digest realm="r"` (no nonce) + Basic: no retry, exit 94.
  - `--anyauth`, `Foo realm="r"`: no retry, exit 0. `--digest`, Basic only: no retry, exit 0. `--basic`, Digest only: `Basic dTpw` sent up front, no retry, exit 0.
- For BL-181 (handler): the retry-without-header after a Negotiate pick and exit 94 for an unreadable Digest challenge are the handler's, from a `null` answer. For BL-237 (Console): compose `RankedHttpAuthenticator`, not `BasicAndBearerAuthenticator` alone.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. RankedHttpAuthenticator picks among offered challenges in libcurl's order (Negotiate, Bearer, Digest, NTLM, Basic) with no fallback, as measured on curl 8.21.0
